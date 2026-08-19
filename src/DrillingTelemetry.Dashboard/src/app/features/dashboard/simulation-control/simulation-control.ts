import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Presents the controls that will update the running telemetry simulation. */
@Component({
  selector: 'app-simulation-control',
  templateUrl: './simulation-control.html',
  styleUrl: './simulation-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SimulationControl {}
