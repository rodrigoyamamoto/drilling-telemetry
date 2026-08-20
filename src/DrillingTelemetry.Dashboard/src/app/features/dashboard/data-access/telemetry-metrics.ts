/** Live processing metrics published by the telemetry Processor. */
export interface TelemetryMetrics {
  readonly sampledAtUtc: string;
  readonly readingsProcessedTotal: number;
  readonly readingsPerSecond: number;
  readonly latestEndToEndLatencyMilliseconds: number | null;
}
