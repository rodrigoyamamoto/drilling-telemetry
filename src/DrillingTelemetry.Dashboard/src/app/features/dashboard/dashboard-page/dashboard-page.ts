import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { TelemetryHistoryService } from '../data-access/telemetry-history.service';
import { DeviceList } from '../device-list/device-list';
import { SimulationControl } from '../simulation-control/simulation-control';
import { TelemetryChart } from '../telemetry-chart/telemetry-chart';

/** Presents the operational overview for the selected drilling context. */
@Component({
  selector: 'app-dashboard-page',
  imports: [DeviceList, SimulationControl, TelemetryChart],
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
}
