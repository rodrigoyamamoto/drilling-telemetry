BEGIN;

ALTER TABLE telemetry_readings
    ADD COLUMN acquisition_mode text;

UPDATE telemetry_readings
SET
    acquisition_mode = 'RealTime',
    payload = payload || jsonb_build_object('AcquisitionMode', 'RealTime');

ALTER TABLE telemetry_readings
    ALTER COLUMN acquisition_mode SET NOT NULL,
    ADD CONSTRAINT ck_telemetry_readings_acquisition_mode
        CHECK
        (
            acquisition_mode IN
            (
                'RealTime',
                'HistoricalImport'
            )
        );

COMMIT;
