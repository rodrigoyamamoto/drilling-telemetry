/** Operational conditions currently produced by the telemetry processor. */
export type OperationalEventType =
  | 'DuplicateReading'
  | 'ConflictingReading'
  | 'SequenceGap'
  | 'OutOfOrderReading'
  | 'InvalidMessage'
  | 'ConcurrentAcquisitionSessions';

/** Severity assigned by the telemetry processor. */
export type OperationalEventSeverity = 'Warning' | 'Critical';

/** Represents a persisted or live operational telemetry event. */
export interface OperationalEvent {
  readonly eventId: string;
  readonly eventType: OperationalEventType;
  readonly severity: OperationalEventSeverity;
  readonly deviceId: string | null;
  readonly acquisitionSessionId: string | null;
  readonly sequenceNumber: number | null;
  readonly previousSequenceNumber: number | null;
  readonly gapSize: number | null;
  readonly message: string;
  readonly occurredAtUtc: string;
}
