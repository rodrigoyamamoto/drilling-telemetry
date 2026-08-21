import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import type { OperationalEvent } from '../data-access/operational-event';

const maximumVisibleEvents = 4;

/** Presents recent persisted and live operational telemetry events. */
@Component({
  selector: 'app-operational-events-panel',
  imports: [DatePipe],
  templateUrl: './operational-events-panel.html',
  styleUrl: './operational-events-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OperationalEventsPanel {
  /** Operational events in reverse chronological order. */
  readonly events = input.required<readonly OperationalEvent[]>();

  /** Indicates whether event history is being loaded. */
  readonly isLoading = input(false);

  /** User-facing event history error. */
  readonly errorMessage = input<string | null>(null);

  /** Requests another event history load. */
  readonly retryRequested = output<void>();

  /** Events visible in the dashboard summary. */
  protected readonly visibleEvents = computed(() => this.events().slice(0, maximumVisibleEvents));

  /** Gets the concise title used for an operational event. */
  protected getTitle(operationalEvent: OperationalEvent): string {
    switch (operationalEvent.eventType) {
      case 'DuplicateReading':
        return 'Duplicate reading ignored';
      case 'ConflictingReading':
        return 'Conflicting telemetry detected';
      case 'SequenceGap':
        return 'Sequence gap detected';
      case 'OutOfOrderReading':
        return 'Out-of-order reading received';
      case 'InvalidMessage':
        return 'Invalid message rejected';
      case 'ConcurrentAcquisitionSessions':
        return 'Concurrent acquisition sessions detected';
    }
  }

  /** Gets the device context and description for an operational event. */
  protected getDescription(operationalEvent: OperationalEvent): string {
    if (operationalEvent.eventType === 'ConcurrentAcquisitionSessions') {
      return operationalEvent.message;
    }

    const identityParts = [
      operationalEvent.deviceId,
      operationalEvent.sequenceNumber === null
        ? null
        : `sequence ${operationalEvent.sequenceNumber}`,
    ].filter((part): part is string => part !== null);

    return identityParts.length === 0
      ? operationalEvent.message
      : `${identityParts.join(' · ')} · ${operationalEvent.message}`;
  }

  /** Gets the compact icon used for an operational event severity. */
  protected getIcon(operationalEvent: OperationalEvent): string {
    return operationalEvent.severity === 'Critical' ? '×' : '!';
  }
}
