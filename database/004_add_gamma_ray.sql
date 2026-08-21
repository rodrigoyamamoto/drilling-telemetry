ALTER TABLE telemetry_readings
    ADD COLUMN gamma_ray_api double precision;

UPDATE telemetry_readings
SET gamma_ray_api = 0,
    payload = payload || jsonb_build_object('GammaRayApi', 0);

ALTER TABLE telemetry_readings
    ALTER COLUMN gamma_ray_api SET NOT NULL,
    ADD CONSTRAINT ck_telemetry_readings_gamma_ray_api
        CHECK
        (
            gamma_ray_api >= 0
            AND gamma_ray_api < 'Infinity'::double precision
        );
