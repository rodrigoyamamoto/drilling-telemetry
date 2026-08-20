import { Injectable, signal } from '@angular/core';
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../../environments/environment';
import type { TelemetryReading } from './telemetry-reading';

const initialRetryDelayMilliseconds = 5_000;
const telemetryReadingReceivedEventName = 'telemetryReadingReceived';

/** Describes the dashboard connection to the telemetry hub. */
export type LiveConnectionStatus =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected';

/** Receives accepted telemetry readings from the Processor SignalR hub. */
@Injectable({ providedIn: 'root' })
export class TelemetryLiveService {
  private readonly connectionStatusSignal = signal<LiveConnectionStatus>('disconnected');
  private readonly connectionEstablishedSubject = new Subject<void>();
  private readonly readingSubject = new Subject<TelemetryReading>();
  private readonly connection: HubConnection;

  private startPromise: Promise<void> | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  /** Current SignalR connection status. */
  readonly connectionStatus = this.connectionStatusSignal.asReadonly();

  /** Stream of telemetry readings accepted by the Processor. */
  readonly readings$ = this.readingSubject.asObservable();

  /** Emits after an initial connection or successful reconnection. */
  readonly connectionEstablished$ = this.connectionEstablishedSubject.asObservable();

  constructor() {
    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.processorApiUrl}/hubs/telemetry`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.registerConnectionHandlers();
  }

  /** Starts the SignalR connection when it is not already active. */
  start(): void {
    if (
      this.connection.state !== HubConnectionState.Disconnected ||
      this.startPromise
    ) {
      return;
    }

    this.clearRetryTimer();
    this.connectionStatusSignal.set('connecting');

    this.startPromise = this.connection
      .start()
      .then(() => this.recordConnected())
      .catch(() => {
        this.connectionStatusSignal.set('disconnected');
        this.scheduleInitialRetry();
      })
      .finally(() => {
        this.startPromise = null;
      });
  }

  private registerConnectionHandlers(): void {
    this.connection.on(
      telemetryReadingReceivedEventName,
      (reading: TelemetryReading) => this.readingSubject.next(reading)
    );

    this.connection.onreconnecting(() => {
      this.connectionStatusSignal.set('reconnecting');
    });

    this.connection.onreconnected(() => {
      this.recordConnected();
    });

    this.connection.onclose(() => {
      this.connectionStatusSignal.set('disconnected');
      this.scheduleInitialRetry();
    });
  }

  private scheduleInitialRetry(): void {
    if (this.retryTimer) {
      return;
    }

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      this.start();
    }, initialRetryDelayMilliseconds);
  }

  private recordConnected(): void {
    this.connectionStatusSignal.set('connected');
    this.connectionEstablishedSubject.next();
  }

  private clearRetryTimer(): void {
    if (!this.retryTimer) {
      return;
    }

    clearTimeout(this.retryTimer);
    this.retryTimer = null;
  }
}
