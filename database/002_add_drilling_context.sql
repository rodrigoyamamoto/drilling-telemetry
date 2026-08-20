ALTER TABLE telemetry_readings
    ADD COLUMN well_id text,
    ADD COLUMN wellbore_id text,
    ADD COLUMN measured_depth_metres double precision;

UPDATE telemetry_readings
SET
    well_id = 'LEGACY-WELL',
    wellbore_id = 'LEGACY-WELLBORE',
    measured_depth_metres = 0;

ALTER TABLE telemetry_readings
    ALTER COLUMN well_id SET NOT NULL,
    ALTER COLUMN wellbore_id SET NOT NULL,
    ALTER COLUMN measured_depth_metres SET NOT NULL,
    ADD CONSTRAINT ck_telemetry_readings_measured_depth
        CHECK (measured_depth_metres >= 0);
