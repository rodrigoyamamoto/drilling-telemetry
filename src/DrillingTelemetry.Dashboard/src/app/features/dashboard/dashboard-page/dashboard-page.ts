import { DatePipe, DecimalPipe } from '@angular/common';
import type { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import { TelemetryHistoryService } from '../data-access/telemetry-history.service';
import type { TelemetryReading } from '../data-access/telemetry-reading';
import { DeviceList } from '../device-list/device-list';
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
  errorMessage: null
};

/** Presents the operational overview for the selected drilling context. */
@Component({
  selector: 'app-dashboard-page',
  imports: [DatePipe, DecimalPipe, DeviceList, SimulationControl, TelemetryChart],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardPage {
  private readonly destroyRef = inject(DestroyRef);
  private readonly telemetryHistoryService = inject(TelemetryHistoryService);

  /** Device identifiers returned by the Processor API. */
  protected readonly deviceIds = signal<readonly string[]>([]);

  /** Identifier selected for the next historical query. */
  protected readonly selectedDeviceId = signal<string | null>(null);

  /** Indicates whether the device request is in progress. */
  protected readonly isLoadingDevices = signal(true);

  /** User-facing error produced by the device request. */
  protected readonly deviceLoadError = signal<string | null>(null);

  /** Historical reading request driven by the selected device. */
  protected readonly readingState = toSignal(
    toObservable(this.selectedDeviceId).pipe(
      distinctUntilChanged(),
      switchMap(deviceId => this.loadReadings(deviceId))
    ),
    { initialValue: initialReadingState }
  );

  /** Most recent reading returned by the historical endpoint. */
  protected readonly latestReading = computed(() => this.readingState().readings.at(-1) ?? null);

  /** Short label describing the historical reading state. */
  protected readonly readingStatusLabel = computed(() => {
    const state = this.readingState();

    switch (state.status) {
      case 'loading':
        return 'Loading';
      case 'error':
        return 'Unavailable';
      case 'loaded':
        return state.readings.length > 0 ? 'Latest sample' : 'No data';
      default:
        return 'Waiting';
    }
  });

  constructor() {
    this.loadDevices();
  }

  /** Loads the devices that have persisted telemetry readings. */
  protected loadDevices(): void {
    this.isLoadingDevices.set(true);
    this.deviceLoadError.set(null);

    this.telemetryHistoryService
      .getDeviceIds()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: deviceIds => {
          this.deviceIds.set(deviceIds);
          this.selectAvailableDevice(deviceIds);
          this.isLoadingDevices.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.deviceLoadError.set(
            error.status === 0
              ? 'The Processor API is unavailable.'
              : 'The available devices could not be loaded.'
          );
          this.isLoadingDevices.set(false);
        }
      });
  }

  /** Selects the device that will provide the dashboard readings. */
  protected selectDevice(deviceId: string): void {
    this.selectedDeviceId.set(deviceId);
  }

  private selectAvailableDevice(deviceIds: readonly string[]): void {
    const selectedDeviceId = this.selectedDeviceId();

    if (selectedDeviceId && deviceIds.includes(selectedDeviceId)) {
      return;
    }

    this.selectedDeviceId.set(deviceIds[0] ?? null);
  }

  private loadReadings(deviceId: string | null): Observable<ReadingState> {
    if (!deviceId) {
      return of(initialReadingState);
    }

    return this.telemetryHistoryService.getReadings(deviceId).pipe(
      map(readings => ({
        status: 'loaded' as const,
        readings,
        errorMessage: null
      })),
      catchError((error: HttpErrorResponse) => of({
        status: 'error' as const,
        readings: [],
        errorMessage: error.status === 0
          ? 'The Processor API is unavailable.'
          : 'Historical readings could not be loaded.'
      })),
      startWith({
        status: 'loading' as const,
        readings: [],
        errorMessage: null
      })
    );
  }
}
