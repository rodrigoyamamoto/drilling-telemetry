import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';

/** Reads persisted telemetry data from the Processor API. */
@Injectable({ providedIn: 'root' })
export class TelemetryHistoryService {
  private readonly httpClient = inject(HttpClient);

  /** Gets the identifiers of devices with persisted telemetry readings. */
  getDeviceIds(): Observable<readonly string[]> {
    return this.httpClient.get<readonly string[]>(
      `${environment.processorApiUrl}/api/telemetry/devices`
    );
  }
}
