import { DatePipe, DecimalPipe } from '@angular/common';
import type { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { OperationalEvent } from '../data-access/operational-event';
import { OperationalEventsService } from '../data-access/operational-events.service';
import { DrillingOperation } from '../data-access/simulation-settings';
import { TelemetryHistoryService } from '../data-access/telemetry-history.service';
import type { TelemetryMetrics } from '../data-access/telemetry-metrics';
import type { TelemetryReading } from '../data-access/telemetry-reading';
import { TelemetryLiveService } from '../data-access/telemetry-live.service';
import { DeviceList } from '../device-list/device-list';
import { GammaRayChart } from '../gamma-ray-chart/gamma-ray-chart';
import { OperationalEventsPanel } from '../operational-events-panel/operational-events-panel';
import { OperationalDepthChart } from '../operational-depth-chart/operational-depth-chart';
import { SimulationControl } from '../simulation-control/simulation-control';
import { TelemetryChart } from '../telemetry-chart/telemetry-chart';

type ReadingLoadStatus = 'idle' | 'loading' | 'loaded' | 'error';

interface ReadingState {
  readonly status: ReadingLoadStatus;
  readonly readings: readonly TelemetryReading[];
  readonly errorMessage: string | null;
}

const initialReadingState: ReadingState = {
  status: 'idle',
  readings: [],
  errorMessage: null,
};

const maximumDisplayedReadings = 100;
const maximumOperationalEvents = 20;

/** Presents the operational overview for the selected drilling context. */
@Component({
  selector: 'app-dashboard-page',
  imports: [
    DatePipe,
    DecimalPipe,
    DeviceList,
    GammaRayChart,
    OperationalDepthChart,
    OperationalEventsPanel,
    SimulationControl,
    TelemetryChart,
  ],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly destroyRef = inject(DestroyRef);
  private readonly operationalEventsService = inject(OperationalEventsService);
  private readonly telemetryHistoryService = inject(TelemetryHistoryService);
  private readonly telemetryLiveService = inject(TelemetryLiveService);

  private readonly liveReadings = signal<readonly TelemetryReading[]>([]);

  /** Latest processing metrics received from the Processor. */
  protected readonly telemetryMetrics = signal<TelemetryMetrics | null>(null);

  /** Persisted and live operational events in reverse chronological order. */
  protected readonly operationalEvents = signal<readonly OperationalEvent[]>([]);

  /** Indicates whether operational events are being loaded. */
  protected readonly isLoadingOperationalEvents = signal(true);

  /** User-facing error produced by the operational event request. */
  protected readonly operationalEventsError = signal<string | null>(null);

  /** Device identifiers returned by the Processor API. */
  protected readonly deviceIds = signal<readonly string[]>([]);

  /** Identifier selected for the next historical query. */
  protected readonly selectedDeviceId = signal<string | null>(null);

  private readonly historyRefreshRevision = signal(0);
  private readonly readingQuery = computed(() => ({
    deviceId: this.selectedDeviceId(),
    revision: this.historyRefreshRevision(),
  }));

  /** Indicates whether the device request is in progress. */
  protected readonly isLoadingDevices = signal(true);

  /** User-facing error produced by the device request. */
  protected readonly deviceLoadError = signal<string | null>(null);

  /** Historical reading request driven by the selected device. */
  protected readonly readingState = toSignal(
    toObservable(this.readingQuery).pipe(switchMap((query) => this.loadReadings(query.deviceId))),
    { initialValue: initialReadingState },
  );

  /** SignalR connection status exposed by the live telemetry service. */
  protected readonly liveConnectionStatus = this.telemetryLiveService.connectionStatus;

  /** Historical baseline merged with accepted live readings, scoped to the active acquisition session. */
  protected readonly displayedReadings = computed(() =>
    this.mergeReadings(this.readingState().readings, this.liveReadings()),
  );

  /** Most recent reading available to the dashboard. */
  protected readonly latestReading = computed(() => this.displayedReadings().at(-1) ?? null);

  /** Identifier of the acquisition run currently displayed. */
  protected readonly activeAcquisitionRunId = computed(
    () => this.latestReading()?.acquisitionSessionId ?? null,
  );

  /** Short, human-readable label for the active acquisition run. */
  protected readonly activeAcquisitionRunLabel = computed(() => {
    const runId = this.activeAcquisitionRunId();

    return runId ? runId.slice(0, 8).toUpperCase() : '—';
  });

  /** Compact name-derived label displayed beside the active well. */
  protected readonly wellMonogram = computed(() => {
    const wellName = this.latestReading()?.wellName;

    return wellName
      ? wellName
          .replaceAll(/[^a-zA-Z0-9]/g, '')
          .slice(-2)
          .toUpperCase()
      : '—';
  });

  /** Human-readable operation received with the latest reading. */
  protected readonly drillingOperationLabel = computed(() => {
    switch (this.latestReading()?.drillingOperation) {
      case DrillingOperation.Stationary:
        return 'Stationary';
      case DrillingOperation.DrillingAhead:
        return 'Drilling ahead';
      case DrillingOperation.TrippingOut:
        return 'Tripping out';
      default:
        return '—';
    }
  });

  /** Short label describing the historical reading state. */
  protected readonly readingStatusLabel = computed(() => {
    const state = this.readingState();

    if (this.displayedReadings().length > 0) {
      return this.liveConnectionStatus() === 'connected' ? 'Live reading' : 'Latest reading';
    }

    switch (state.status) {
      case 'loading':
        return 'Loading';
      case 'error':
        return 'Unavailable';
      case 'loaded':
        return 'No data';
      default:
        return 'Waiting';
    }
  });

  /** User-facing SignalR connection label. */
  protected readonly liveConnectionLabel = computed(() => {
    switch (this.liveConnectionStatus()) {
      case 'connected':
        return 'Live';
      case 'connecting':
        return 'Connecting';
      case 'reconnecting':
        return 'Reconnecting';
      default:
        return 'Offline';
    }
  });

  constructor() {
    this.loadDevices();
    this.loadOperationalEvents();

    this.telemetryLiveService.readings$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((reading) => this.receiveLiveReading(reading));

    this.telemetryLiveService.operationalEvents$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((operationalEvent) => this.receiveOperationalEvent(operationalEvent));

    this.telemetryLiveService.metrics$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((metrics) => this.telemetryMetrics.set(metrics));

    this.telemetryLiveService.connectionEstablished$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refreshHistoryAfterConnection());

    this.telemetryLiveService.start();
  }

  /** Loads the devices that have persisted telemetry readings. */
  protected loadDevices(): void {
    this.isLoadingDevices.set(true);
    this.deviceLoadError.set(null);

    this.telemetryHistoryService
      .getDeviceIds()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (deviceIds) => {
          this.deviceIds.update((currentDeviceIds) =>
            [...new Set([...currentDeviceIds, ...deviceIds])].sort((left, right) =>
              left.localeCompare(right),
            ),
          );
          this.selectAvailableDevice(this.deviceIds());
          this.isLoadingDevices.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.deviceLoadError.set(
            error.status === 0
              ? 'The Processor API is unavailable.'
              : 'The available devices could not be loaded.',
          );
          this.isLoadingDevices.set(false);
        },
      });
  }

  /** Loads recent operational events from the Processor API. */
  protected loadOperationalEvents(): void {
    this.isLoadingOperationalEvents.set(true);
    this.operationalEventsError.set(null);

    this.operationalEventsService
      .getRecentEvents(maximumOperationalEvents)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (operationalEvents) => {
          this.operationalEvents.update((currentEvents) =>
            this.mergeOperationalEvents(currentEvents, operationalEvents),
          );
          this.isLoadingOperationalEvents.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.operationalEventsError.set(
            error.status === 0
              ? 'The Processor API is unavailable.'
              : 'Operational events could not be loaded.',
          );
          this.isLoadingOperationalEvents.set(false);
        },
      });
  }

  /** Selects the device that will provide the dashboard readings. */
  protected selectDevice(deviceId: string): void {
    if (this.selectedDeviceId() === deviceId) {
      return;
    }

    this.liveReadings.set([]);
    this.selectedDeviceId.set(deviceId);
  }

  private selectAvailableDevice(deviceIds: readonly string[]): void {
    const selectedDeviceId = this.selectedDeviceId();

    if (selectedDeviceId && deviceIds.includes(selectedDeviceId)) {
      return;
    }

    this.liveReadings.set([]);
    this.selectedDeviceId.set(deviceIds[0] ?? null);
  }

  private loadReadings(deviceId: string | null): Observable<ReadingState> {
    if (!deviceId) {
      return of(initialReadingState);
    }

    return this.telemetryHistoryService.getReadings(deviceId).pipe(
      map((readings) => ({
        status: 'loaded' as const,
        readings,
        errorMessage: null,
      })),
      catchError((error: HttpErrorResponse) =>
        of({
          status: 'error' as const,
          readings: [],
          errorMessage:
            error.status === 0
              ? 'The Processor API is unavailable.'
              : 'Historical readings could not be loaded.',
        }),
      ),
      startWith({
        status: 'loading' as const,
        readings: [],
        errorMessage: null,
      }),
    );
  }

  private receiveLiveReading(reading: TelemetryReading): void {
    this.deviceIds.update((deviceIds) => {
      if (deviceIds.includes(reading.deviceId)) {
        return deviceIds;
      }

      return [...deviceIds, reading.deviceId].sort((left, right) => left.localeCompare(right));
    });

    if (reading.deviceId !== this.selectedDeviceId()) {
      return;
    }

    this.liveReadings.update((readings) => this.mergeReadings(readings, [reading]));
  }

  private receiveOperationalEvent(operationalEvent: OperationalEvent): void {
    this.operationalEvents.update((operationalEvents) =>
      this.mergeOperationalEvents(operationalEvents, [operationalEvent]),
    );
  }

  private refreshHistoryAfterConnection(): void {
    this.loadOperationalEvents();

    if (this.selectedDeviceId()) {
      this.historyRefreshRevision.update((revision) => revision + 1);
    }
  }

  private mergeReadings(
    baseline: readonly TelemetryReading[],
    incoming: readonly TelemetryReading[],
  ): readonly TelemetryReading[] {
    const readingsByIdentity = new Map<string, TelemetryReading>();

    for (const reading of [...baseline, ...incoming]) {
      readingsByIdentity.set(this.createReadingIdentity(reading), reading);
    }

    const deduplicatedReadings = [...readingsByIdentity.values()];

    if (deduplicatedReadings.length === 0) {
      return [];
    }

    const activeSessionId = this.findActiveSessionId(deduplicatedReadings);

    return deduplicatedReadings
      .filter((reading) => reading.acquisitionSessionId === activeSessionId)
      .sort((left, right) => this.compareReadings(left, right))
      .slice(-maximumDisplayedReadings);
  }

  private findActiveSessionId(readings: readonly TelemetryReading[]): string {
    let latestReading = readings[0];

    for (const reading of readings.slice(1)) {
      const comparison = this.compareReadings(reading, latestReading);

      if (comparison >= 0) {
        latestReading = reading;
      }
    }

    return latestReading.acquisitionSessionId;
  }

  private createReadingIdentity(reading: TelemetryReading): string {
    return `${reading.deviceId}:${reading.acquisitionSessionId}:${reading.sequenceNumber}`;
  }

  private compareReadings(left: TelemetryReading, right: TelemetryReading): number {
    const timestampDifference = Date.parse(left.timestampUtc) - Date.parse(right.timestampUtc);

    return timestampDifference || left.sequenceNumber - right.sequenceNumber;
  }

  private mergeOperationalEvents(
    baseline: readonly OperationalEvent[],
    incoming: readonly OperationalEvent[],
  ): readonly OperationalEvent[] {
    const eventsById = new Map<string, OperationalEvent>();

    for (const operationalEvent of [...baseline, ...incoming]) {
      eventsById.set(operationalEvent.eventId, operationalEvent);
    }

    return [...eventsById.values()]
      .sort((left, right) => Date.parse(right.occurredAtUtc) - Date.parse(left.occurredAtUtc))
      .slice(0, maximumOperationalEvents);
  }
}
