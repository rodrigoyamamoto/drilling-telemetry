BEGIN;

ALTER TABLE telemetry_readings
    ADD COLUMN IF NOT EXISTS well_name text,
    ADD COLUMN IF NOT EXISTS wellbore_name text;

UPDATE telemetry_readings
SET
    well_name = COALESCE(
        NULLIF(well_name, ''),
        'Legacy well (' || well_id || ')'),
    wellbore_name = COALESCE(
        NULLIF(wellbore_name, ''),
        'Legacy wellbore (' || wellbore_id || ')'),
    payload = payload || jsonb_build_object(
        'WellName',
        COALESCE(
            NULLIF(well_name, ''),
            'Legacy well (' || well_id || ')'),
        'WellboreName',
        COALESCE(
            NULLIF(wellbore_name, ''),
            'Legacy wellbore (' || wellbore_id || ')'));

ALTER TABLE telemetry_readings
    ALTER COLUMN well_name SET NOT NULL,
    ALTER COLUMN wellbore_name SET NOT NULL;

COMMIT;
