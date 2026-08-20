#!/usr/bin/env bash

# Runs a repeatable Processor outage and recovery scenario.
# Stop backend Rider configurations before running this script.
set -Eeuo pipefail

readonly SCRIPT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPOSITORY_ROOT="$(cd "${SCRIPT_DIRECTORY}/../.." && pwd)"

readonly CONTROL_API_URL="${CONTROL_API_URL:-http://localhost:5153}"
readonly PROCESSOR_URL="${PROCESSOR_URL:-http://localhost:5154}"
readonly OUTAGE_SECONDS="${OUTAGE_SECONDS:-20}"
readonly WARM_UP_SECONDS="${WARM_UP_SECONDS:-10}"
readonly RECOVERY_TIMEOUT_SECONDS="${RECOVERY_TIMEOUT_SECONDS:-60}"
readonly LOAD_TEST_DURATION="${LOAD_TEST_DURATION:-1m}"
readonly TELEMETRY_QUEUE="drilling.telemetry.readings"
readonly TARGET_FRAMEWORK="net10.0"

readonly RUN_IDENTIFIER="$(date -u +%Y%m%dT%H%M%SZ)"
readonly RESULTS_DIRECTORY="${REPOSITORY_ROOT}/artifacts/recovery-tests/${RUN_IDENTIFIER}"

control_api_pid=""
processor_pid=""
simulator_pid=""

cleanup() {
    stop_process "${simulator_pid}"
    stop_process "${processor_pid}"
    stop_process "${control_api_pid}"
}

stop_process() {
    local process_id="${1:-}"
    local attempt

    if [[ -n "${process_id}" ]] && kill -0 "${process_id}" 2>/dev/null; then
        kill -TERM "${process_id}" 2>/dev/null || true

        for attempt in {1..10}; do
            if ! kill -0 "${process_id}" 2>/dev/null; then
                wait "${process_id}" 2>/dev/null || true
                return
            fi

            sleep 1
        done

        kill -KILL "${process_id}" 2>/dev/null || true
        wait "${process_id}" 2>/dev/null || true
    fi
}

require_command() {
    local command_name="$1"

    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Required command '${command_name}' was not found." >&2
        exit 1
    fi
}

wait_for_url() {
    local url="$1"
    local description="$2"
    local attempt

    for attempt in {1..60}; do
        if curl --fail --silent --output /dev/null "${url}"; then
            return
        fi

        sleep 1
    done

    echo "Timed out waiting for ${description} at ${url}." >&2
    exit 1
}

ensure_url_is_not_in_use() {
    local url="$1"
    local description="$2"

    if curl --silent --output /dev/null "${url}"; then
        echo "${description} is already running at ${url}." >&2
        echo "Stop the Rider run configuration before this scenario." >&2
        exit 1
    fi
}

wait_for_infrastructure() {
    local attempt

    for attempt in {1..60}; do
        if docker compose exec -T rabbitmq \
            rabbitmq-diagnostics -q ping >/dev/null 2>&1 && \
            docker compose exec -T postgres \
            pg_isready -U drilling_telemetry \
            -d drilling_telemetry >/dev/null 2>&1; then
            return
        fi

        sleep 1
    done

    echo "RabbitMQ or PostgreSQL did not become ready." >&2
    exit 1
}

get_ready_message_count() {
    docker compose exec -T rabbitmq \
        rabbitmqctl list_queues name messages_ready \
        --formatter csv 2>/dev/null |
        awk -F, -v queue="${TELEMETRY_QUEUE}" '
            NR > 1 {
                gsub(/"/, "", $1)
                gsub(/["\r]/, "", $2)

                if ($1 == queue) {
                    print $2
                }
            }'
}

wait_for_empty_queue() {
    local deadline=$((SECONDS + RECOVERY_TIMEOUT_SECONDS))
    local ready_count

    while ((SECONDS < deadline)); do
        ready_count="$(get_ready_message_count)"

        if [[ "${ready_count:-0}" == "0" ]]; then
            return
        fi

        sleep 1
    done

    echo "The telemetry queue did not drain within " \
        "${RECOVERY_TIMEOUT_SECONDS} seconds." >&2
    return 1
}

start_control_api() {
    (
        cd src/DrillingTelemetry.Control.Api
        export ASPNETCORE_ENVIRONMENT=Development
        export ASPNETCORE_URLS="${CONTROL_API_URL}"
        exec "./bin/Debug/${TARGET_FRAMEWORK}/DrillingTelemetry.Control.Api"
    ) >"${RESULTS_DIRECTORY}/control-api.log" 2>&1 &

    control_api_pid=$!
}

start_processor() {
    local log_file="$1"

    (
        cd src/DrillingTelemetry.Processor
        export ASPNETCORE_ENVIRONMENT=Development
        export ASPNETCORE_URLS="${PROCESSOR_URL}"
        exec "./bin/Debug/${TARGET_FRAMEWORK}/DrillingTelemetry.Processor"
    ) >"${log_file}" 2>&1 &

    processor_pid=$!
}

start_simulator() {
    (
        cd src/DrillingTelemetry.DeviceSimulator
        exec "./bin/Debug/${TARGET_FRAMEWORK}/DrillingTelemetry.DeviceSimulator" \
            --Simulation:GenerationMode=Random
    ) >"${RESULTS_DIRECTORY}/device-simulator.log" 2>&1 &

    simulator_pid=$!
}

apply_load_settings() {
    curl --fail --silent --show-error \
        --request POST \
        --header "Content-Type: application/json" \
        --data '{
            "deviceIds": [
                "DRILL-001", "DRILL-002", "DRILL-003",
                "DRILL-004", "DRILL-005", "DRILL-006",
                "DRILL-007", "DRILL-008", "DRILL-009",
                "DRILL-010", "DRILL-011", "DRILL-012"
            ],
            "publishingIntervalMilliseconds": 250
        }' \
        "${CONTROL_API_URL}/api/simulation/settings" \
        >"${RESULTS_DIRECTORY}/simulation-settings-response.json"
}

run_k6() {
    PROCESSOR_BASE_URL="${PROCESSOR_URL}" \
    DURATION="${LOAD_TEST_DURATION}" \
    k6 run \
        --summary-export "${RESULTS_DIRECTORY}/k6-summary.json" \
        tests/load/processor-read-api.js \
        2>&1 | tee "${RESULTS_DIRECTORY}/k6-output.txt"
}

capture_database_evidence() {
    docker compose exec -T postgres \
        psql -X -U drilling_telemetry -d drilling_telemetry \
        --set ON_ERROR_STOP=1 \
        --set test_started_at="${test_started_at}" \
        --file - \
        >"${RESULTS_DIRECTORY}/database-evidence.txt" <<'SQL'
\pset pager off

SELECT
    acquisition_session_id,
    COUNT(*) AS reading_count,
    COUNT(DISTINCT device_id) AS device_count,
    ROUND(
        COUNT(*) /
        NULLIF(
            EXTRACT(EPOCH FROM (
                MAX(received_at_utc) - MIN(received_at_utc)
            )),
            0
        ),
        2
    ) AS average_readings_per_second,
    MIN(received_at_utc) AS started_at,
    MAX(received_at_utc) AS finished_at
FROM telemetry_readings
WHERE received_at_utc >= :'test_started_at'::timestamptz
GROUP BY acquisition_session_id
ORDER BY MAX(received_at_utc) DESC
LIMIT 1;

WITH latest_session AS
(
    SELECT acquisition_session_id
    FROM telemetry_readings
    WHERE received_at_utc >= :'test_started_at'::timestamptz
    GROUP BY acquisition_session_id
    ORDER BY MAX(received_at_utc) DESC
    LIMIT 1
)
SELECT
    reading.device_id,
    MIN(reading.sequence_number) AS minimum_sequence,
    MAX(reading.sequence_number) AS maximum_sequence,
    COUNT(*) AS reading_count,
    MAX(reading.sequence_number) -
        MIN(reading.sequence_number) + 1 - COUNT(*) AS missing_sequences
FROM telemetry_readings AS reading
INNER JOIN latest_session AS session
    ON session.acquisition_session_id =
        reading.acquisition_session_id
GROUP BY reading.device_id
ORDER BY reading.device_id;

SELECT
    event_type,
    COUNT(*) AS event_count
FROM operational_events
WHERE occurred_at_utc >= :'test_started_at'::timestamptz
GROUP BY event_type
ORDER BY event_type;

SELECT
    COUNT(*) FILTER (
        WHERE event_type = 'SequenceGap'
    ) AS sequence_gap_events,
    COUNT(*) FILTER (
        WHERE event_type = 'OutOfOrderReading'
    ) AS out_of_order_events
FROM operational_events
WHERE occurred_at_utc >= :'test_started_at'::timestamptz;
SQL
}

capture_rabbitmq_evidence() {
    local output_file="$1"

    docker compose exec -T rabbitmq \
        rabbitmqctl list_queues \
        name messages_ready messages_unacknowledged consumers \
        >"${output_file}"
}

write_summary() {
    local backlog_count="$1"
    local recovered_count="$2"

    {
        echo "Processor recovery scenario"
        echo "Run: ${RUN_IDENTIFIER}"
        echo "Started (UTC): ${test_started_at}"
        echo "Devices: 12"
        echo "Publishing interval: 250 ms"
        echo "Expected ingestion rate: approximately 48 msg/s"
        echo "Processor outage: ${OUTAGE_SECONDS} s"
        echo "Backlog after outage: ${backlog_count} ready messages"
        echo "Ready messages after recovery: ${recovered_count}"
        echo "k6 duration during recovery: ${LOAD_TEST_DURATION}"
        echo
        echo "Inspect database-evidence.txt for sequence continuity."
        echo "Inspect processor-recovery.log for ordering warnings."
        echo "Inspect k6-output.txt and k6-summary.json for API latency."
    } >"${RESULTS_DIRECTORY}/summary.txt"
}

main() {
    require_command curl
    require_command docker
    require_command dotnet
    require_command k6

    mkdir -p "${RESULTS_DIRECTORY}"
    trap cleanup EXIT INT TERM

    cd "${REPOSITORY_ROOT}"

    ensure_url_is_not_in_use \
        "${CONTROL_API_URL}/openapi/v1.json" \
        "The control API"

    ensure_url_is_not_in_use \
        "${PROCESSOR_URL}/api/telemetry/devices" \
        "The Processor API"

    if [[ ! -f .env ]]; then
        echo "The repository .env file is required for PostgreSQL." >&2
        exit 1
    fi

    echo "Building the solution..."
    dotnet build DrillingTelemetry.sln \
        --disable-build-servers \
        -m:1 \
        -p:EnforceCodeStyleInBuild=true \
        >"${RESULTS_DIRECTORY}/build.log"

    echo "Starting RabbitMQ and PostgreSQL..."
    docker compose up -d rabbitmq postgres \
        >"${RESULTS_DIRECTORY}/docker-compose.log"

    wait_for_infrastructure

    test_started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    readonly test_started_at

    echo "Starting the control API and Processor..."
    start_control_api
    start_processor "${RESULTS_DIRECTORY}/processor-initial.log"

    wait_for_url "${CONTROL_API_URL}/openapi/v1.json" "control API"
    wait_for_url "${PROCESSOR_URL}/api/telemetry/devices" "Processor API"

    echo "Starting the simulator and applying the load profile..."
    start_simulator
    sleep 2
    apply_load_settings
    sleep "${WARM_UP_SECONDS}"

    echo "Stopping the Processor for ${OUTAGE_SECONDS} seconds..."
    stop_process "${processor_pid}"
    processor_pid=""
    sleep "${OUTAGE_SECONDS}"

    local backlog_count
    backlog_count="$(get_ready_message_count)"
    capture_rabbitmq_evidence \
        "${RESULTS_DIRECTORY}/rabbitmq-during-outage.txt"

    echo "Backlog captured: ${backlog_count:-0} ready messages."
    echo "Restarting the Processor and running k6 during recovery..."

    start_processor "${RESULTS_DIRECTORY}/processor-recovery.log"
    wait_for_url "${PROCESSOR_URL}/api/telemetry/devices" "Processor API"

    run_k6

    local queue_drained=true
    if ! wait_for_empty_queue; then
        queue_drained=false
    fi

    local recovered_count
    recovered_count="$(get_ready_message_count)"

    capture_rabbitmq_evidence \
        "${RESULTS_DIRECTORY}/rabbitmq-after-recovery.txt"
    capture_database_evidence
    write_summary "${backlog_count:-0}" "${recovered_count:-0}"

    if [[ "${queue_drained}" != "true" ]]; then
        echo "Recovery did not complete within the configured timeout." >&2
        exit 1
    fi

    echo
    echo "Recovery scenario completed."
    echo "Evidence: ${RESULTS_DIRECTORY}"
    echo
    cat "${RESULTS_DIRECTORY}/summary.txt"
}

main "$@"
