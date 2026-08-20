import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Displays the devices with persisted telemetry readings. */
@Component({
  selector: 'app-device-list',
  templateUrl: './device-list.html',
  styleUrl: './device-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DeviceList {
  /** Device identifiers available for historical queries. */
  readonly deviceIds = input.required<readonly string[]>();

  /** Identifier currently selected by the dashboard. */
  readonly selectedDeviceId = input<string | null>(null);

  /** Indicates whether devices are being loaded. */
  readonly isLoading = input(false);

  /** User-facing loading error, or null when no error exists. */
  readonly errorMessage = input<string | null>(null);

  /** Emits when the operator selects a device. */
  readonly deviceSelected = output<string>();

  /** Emits when the operator requests another loading attempt. */
  readonly retryRequested = output<void>();

  /** Selects a device from the available list. */
  protected selectDevice(deviceId: string): void {
    this.deviceSelected.emit(deviceId);
  }
}
