/** Settings accepted by the Control API for the running simulation. */
export interface SimulationSettingsUpdate {
  readonly deviceIds: readonly string[];
  readonly publishingIntervalMilliseconds: number;
}

/** Response returned after the Control API accepts a settings update. */
export interface SimulationSettingsUpdateResponse {
  readonly revision: number;
}
