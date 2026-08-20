import type { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';

import { SimulationControlService } from '../data-access/simulation-control.service';

type SettingsUpdateStatus = 'idle' | 'submitting' | 'accepted' | 'error';

interface HttpValidationProblemDetails {
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

const minimumPublishingIntervalMilliseconds = 50;
const maximumPublishingIntervalMilliseconds = 5_000;
const initialPublishingIntervalMilliseconds = 500;
const initialDeviceIdentifiers = 'DRILL-001\nDRILL-002\nDRILL-003';

/** Presents the controls that will update the running telemetry simulation. */
@Component({
  selector: 'app-simulation-control',
  imports: [ReactiveFormsModule],
  templateUrl: './simulation-control.html',
  styleUrl: './simulation-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SimulationControl {
  private readonly destroyRef = inject(DestroyRef);
  private readonly simulationControlService = inject(SimulationControlService);

  /** Form used to prepare a runtime settings update. */
  protected readonly settingsForm = new FormGroup({
    deviceIdentifiers: new FormControl(initialDeviceIdentifiers, {
      nonNullable: true,
      validators: [Validators.required]
    }),
    publishingIntervalMilliseconds: new FormControl(
      initialPublishingIntervalMilliseconds,
      {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.min(minimumPublishingIntervalMilliseconds),
          Validators.max(maximumPublishingIntervalMilliseconds)
        ]
      }
    )
  });

  /** Minimum interval supported by the backend contract. */
  protected readonly minimumInterval = minimumPublishingIntervalMilliseconds;

  /** Maximum interval offered by this dashboard control. */
  protected readonly maximumInterval = maximumPublishingIntervalMilliseconds;

  /** Current settings update state. */
  protected readonly updateStatus = signal<SettingsUpdateStatus>('idle');

  /** User-facing result of the latest settings request. */
  protected readonly updateMessage = signal<string | null>(null);

  /** Indicates whether the form differs from the latest accepted request. */
  protected readonly hasChanges = signal(true);

  constructor() {
    this.settingsForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.hasChanges.set(true);
        this.updateStatus.set('idle');
        this.updateMessage.set(null);
      });
  }

  /** Sends the validated settings to the Control API. */
  protected applySettings(): void {
    if (this.updateStatus() === 'submitting') {
      return;
    }

    const deviceIds = this.parseDeviceIdentifiers(
      this.settingsForm.controls.deviceIdentifiers.value
    );

    if (this.settingsForm.invalid || deviceIds.length === 0) {
      this.settingsForm.markAllAsTouched();
      this.updateStatus.set('error');
      this.updateMessage.set('Provide at least one device and a valid interval.');
      return;
    }

    this.updateStatus.set('submitting');
    this.updateMessage.set(null);

    this.simulationControlService
      .updateSettings({
        deviceIds,
        publishingIntervalMilliseconds:
          this.settingsForm.controls.publishingIntervalMilliseconds.value
      })
      .pipe(
        finalize(() => {
          if (this.updateStatus() === 'submitting') {
            this.updateStatus.set('idle');
          }
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: () => {
          this.hasChanges.set(false);
          this.updateStatus.set('accepted');
          this.updateMessage.set('Update accepted by the Control API.');
        },
        error: (error: HttpErrorResponse) => {
          this.updateStatus.set('error');
          this.updateMessage.set(this.getErrorMessage(error));
        }
      });
  }

  private parseDeviceIdentifiers(value: string): readonly string[] {
    return [...new Set(
      value
        .split(/[\n,]+/u)
        .map(deviceId => deviceId.trim())
        .filter(deviceId => deviceId.length > 0)
    )];
  }

  private getErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'The Control API is unavailable.';
    }

    const problemDetails = error.error as HttpValidationProblemDetails | null;
    const validationErrors = problemDetails?.errors;
    const firstValidationMessage = validationErrors
      ? Object.values(validationErrors).flat().at(0)
      : null;

    return firstValidationMessage ?? 'The settings update could not be accepted.';
  }
}
