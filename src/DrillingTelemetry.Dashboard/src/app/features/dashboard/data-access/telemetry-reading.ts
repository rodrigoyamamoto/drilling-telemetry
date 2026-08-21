import type { DrillingOperation } from './simulation-settings';

/** Describes how a telemetry reading was acquired. */
export enum TelemetryAcquisitionMode {
  RealTime = 'RealTime',
  HistoricalImport = 'HistoricalImport',
}

/** Persisted telemetry reading returned by the Processor API. */
export interface TelemetryReading {
  /** Identifier of the telemetry device. */
  readonly deviceId: string;

  /** Acquisition run that owns the sequence number. */
  readonly acquisitionSessionId: string;

  /** Sequence number assigned during acquisition. */
  readonly sequenceNumber: number;

  /** Identifier of the well being drilled. */
  readonly wellId: string;

  /** Name of the well being drilled. */
  readonly wellName: string;

  /** Identifier of the wellbore containing the tool. */
  readonly wellboreId: string;

  /** Name of the wellbore containing the tool. */
  readonly wellboreName: string;

  /** Distance travelled along the wellbore, in metres. */
  readonly measuredDepthMetres: number;

  /** Operation active when the reading was acquired. */
  readonly drillingOperation: DrillingOperation;

  /** Signed measured-depth change rate, in metres per hour. */
  readonly depthChangeRateMetresPerHour: number;

  /** Pressure in pounds per square inch. */
  readonly pressurePsi: number;

  /** Temperature in degrees Celsius. */
  readonly temperatureCelsius: number;

  /** Natural gamma ray measurement, in gAPI. */
  readonly gammaRayApi: number;

  /** UTC acquisition timestamp in ISO 8601 format. */
  readonly timestampUtc: string;

  /** Describes how the reading was acquired. */
  readonly acquisitionMode: TelemetryAcquisitionMode;
}
