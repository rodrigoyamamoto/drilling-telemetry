import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Displays the live pressure and temperature trends. */
@Component({
  selector: 'app-telemetry-chart',
  templateUrl: './telemetry-chart.html',
  styleUrl: './telemetry-chart.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TelemetryChart {}
