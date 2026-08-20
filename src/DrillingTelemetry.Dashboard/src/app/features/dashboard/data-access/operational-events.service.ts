import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import type { OperationalEvent } from './operational-event';

/** Reads persisted operational telemetry events from the Processor API. */
@Injectable({ providedIn: 'root' })
export class OperationalEventsService {
  private readonly httpClient = inject(HttpClient);

  /** Gets recent operational events in reverse chronological order. */
  getRecentEvents(limit = 20): Observable<readonly OperationalEvent[]> {
    return this.httpClient.get<readonly OperationalEvent[]>(
      `${environment.processorApiUrl}/api/telemetry/events`,
      { params: { limit } }
    );
  }
}
