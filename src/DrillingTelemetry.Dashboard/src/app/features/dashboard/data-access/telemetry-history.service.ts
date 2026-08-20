import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import type { TelemetryReading } from './telemetry-reading';

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

  /** Gets the most recent persisted readings for a device in chronological order. */
  getReadings(deviceId: string, limit = 100): Observable<readonly TelemetryReading[]> {
    return this.httpClient.get<readonly TelemetryReading[]>(
      `${environment.processorApiUrl}/api/telemetry/readings/${encodeURIComponent(deviceId)}`,
      { params: { limit } }
    );
  }
}
