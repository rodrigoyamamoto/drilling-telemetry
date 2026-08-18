CREATE TABLE IF NOT EXISTS telemetry_readings
(
    device_id            text             NOT NULL,
    acquisition_session_id uuid           NOT NULL,
    sequence_number      bigint           NOT NULL,
    pressure_psi         double precision NOT NULL,
    temperature_celsius  double precision NOT NULL,
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
        CHECK (sequence_number > 0)
);

CREATE INDEX IF NOT EXISTS ix_telemetry_readings_device_timestamp
    ON telemetry_readings (device_id, timestamp_utc);
