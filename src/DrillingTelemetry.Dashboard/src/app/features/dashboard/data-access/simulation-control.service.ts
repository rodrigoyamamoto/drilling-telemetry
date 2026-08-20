import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import type {
  SimulationSettingsUpdate,
  SimulationSettingsUpdateResponse
} from './simulation-settings';

/** Sends runtime simulation settings to the Control API. */
@Injectable({ providedIn: 'root' })
export class SimulationControlService {
  private readonly httpClient = inject(HttpClient);

  /** Requests a settings update without restarting the simulator. */
  updateSettings(
    settings: SimulationSettingsUpdate
  ): Observable<SimulationSettingsUpdateResponse> {
    return this.httpClient.post<SimulationSettingsUpdateResponse>(
      `${environment.controlApiUrl}/api/simulation/settings`,
      settings
    );
  }
}
