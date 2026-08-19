import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Displays the active telemetry devices and stream integrity summary. */
@Component({
  selector: 'app-device-list',
  templateUrl: './device-list.html',
  styleUrl: './device-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DeviceList {}
