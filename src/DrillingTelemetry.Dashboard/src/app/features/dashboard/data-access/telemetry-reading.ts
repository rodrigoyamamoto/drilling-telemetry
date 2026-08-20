/** Persisted telemetry reading returned by the Processor API. */
export interface TelemetryReading {
  /** Identifier of the telemetry device. */
  readonly deviceId: string;

  /** Acquisition session that owns the sequence number. */
  readonly acquisitionSessionId: string;

  /** Sequence number assigned during acquisition. */
  readonly sequenceNumber: number;

  /** Pressure in pounds per square inch. */
  readonly pressurePsi: number;

  /** Temperature in degrees Celsius. */
  readonly temperatureCelsius: number;

  /** UTC acquisition timestamp in ISO 8601 format. */
  readonly timestampUtc: string;
}
