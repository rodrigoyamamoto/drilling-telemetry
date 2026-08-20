ALTER TABLE telemetry_readings
    ADD COLUMN drilling_operation text,
    ADD COLUMN depth_change_rate_metres_per_hour double precision;

UPDATE telemetry_readings
SET
    drilling_operation = 'Stationary',
    depth_change_rate_metres_per_hour = 0;

ALTER TABLE telemetry_readings
    ALTER COLUMN drilling_operation SET NOT NULL,
    ALTER COLUMN depth_change_rate_metres_per_hour SET NOT NULL,
    ADD CONSTRAINT ck_telemetry_readings_measured_depth_finite
        CHECK
        (
            measured_depth_metres < 'Infinity'::double precision
        ),
    ADD CONSTRAINT ck_telemetry_readings_drilling_operation
        CHECK
        (
            drilling_operation IN
            (
                'Stationary',
                'DrillingAhead',
                'TrippingOut'
            )
        ),
    ADD CONSTRAINT ck_telemetry_readings_depth_change_rate
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
        );
