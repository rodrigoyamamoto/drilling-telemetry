CREATE TABLE IF NOT EXISTS telemetry_readings
(
    device_id            text             NOT NULL,
    acquisition_session_id uuid           NOT NULL,
    sequence_number      bigint           NOT NULL,
    well_id              text             NOT NULL,
    well_name            text             NOT NULL,
    wellbore_id          text             NOT NULL,
    wellbore_name        text             NOT NULL,
    measured_depth_metres double precision NOT NULL,
    drilling_operation   text             NOT NULL,
    depth_change_rate_metres_per_hour double precision NOT NULL,
    pressure_psi         double precision NOT NULL,
    temperature_celsius  double precision NOT NULL,
    gamma_ray_api        double precision NOT NULL,
    timestamp_utc        timestamp with time zone NOT NULL,
    payload              jsonb            NOT NULL,
    received_at_utc      timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_telemetry_readings
        PRIMARY KEY
        (
            device_id,
            acquisition_session_id,
            sequence_number
        ),

    CONSTRAINT ck_telemetry_readings_sequence_number
        CHECK (sequence_number > 0),

    CONSTRAINT ck_telemetry_readings_measured_depth
        CHECK
        (
            measured_depth_metres >= 0
            AND measured_depth_metres < 'Infinity'::double precision
        ),

    CONSTRAINT ck_telemetry_readings_drilling_operation
        CHECK
        (
            drilling_operation IN
            (
                'Stationary',
                'DrillingAhead',
                'TrippingOut'
            )
        ),

    CONSTRAINT ck_telemetry_readings_depth_change_rate
        CHECK
        (
            depth_change_rate_metres_per_hour >
                '-Infinity'::double precision
            AND depth_change_rate_metres_per_hour <
                'Infinity'::double precision
            AND
            ((drilling_operation = 'Stationary'
                AND depth_change_rate_metres_per_hour = 0)
            OR (drilling_operation = 'DrillingAhead'
                AND depth_change_rate_metres_per_hour > 0)
            OR (drilling_operation = 'TrippingOut'
                AND depth_change_rate_metres_per_hour < 0))
        ),

    CONSTRAINT ck_telemetry_readings_gamma_ray_api
        CHECK
        (
            gamma_ray_api >= 0
            AND gamma_ray_api < 'Infinity'::double precision
        )
);

CREATE INDEX IF NOT EXISTS ix_telemetry_readings_device_timestamp
    ON telemetry_readings (device_id, timestamp_utc);

CREATE TABLE IF NOT EXISTS operational_events
(
    event_id                 uuid                     NOT NULL,
    event_type               text                     NOT NULL,
    severity                 text                     NOT NULL,
    device_id                text,
    acquisition_session_id   uuid,
    sequence_number          bigint,
    previous_sequence_number bigint,
    gap_size                 bigint,
    message                  text                     NOT NULL,
    occurred_at_utc          timestamp with time zone NOT NULL,

    CONSTRAINT pk_operational_events
        PRIMARY KEY (event_id),

    CONSTRAINT ck_operational_events_sequence_number
        CHECK (sequence_number IS NULL OR sequence_number > 0),

    CONSTRAINT ck_operational_events_gap_size
        CHECK (gap_size IS NULL OR gap_size > 0)
);

CREATE INDEX IF NOT EXISTS ix_operational_events_occurred_at
    ON operational_events (occurred_at_utc DESC);
