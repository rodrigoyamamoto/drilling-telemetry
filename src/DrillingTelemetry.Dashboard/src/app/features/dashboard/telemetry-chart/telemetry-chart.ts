import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  viewChild,
} from '@angular/core';
import type { ElementRef, OnDestroy } from '@angular/core';
import {
  CategoryScale,
  Chart,
  Filler,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Tooltip,
} from 'chart.js';
import type { ChartConfiguration, ChartData } from 'chart.js';

import type { TelemetryReading } from '../data-access/telemetry-reading';

Chart.register(
  CategoryScale,
  Filler,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Tooltip,
);

const chartColours = {
  amber: '#f5ae51',
  border: 'rgba(143, 163, 181, 0.12)',
  cyan: '#32d6d0',
  muted: '#8fa3b5',
  surface: '#0c1721',
} as const;

/** Displays persisted pressure and temperature trends. */
@Component({
  selector: 'app-telemetry-chart',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './telemetry-chart.html',
  styleUrl: './telemetry-chart.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TelemetryChart implements OnDestroy {
  private readonly chartCanvas = viewChild<ElementRef<HTMLCanvasElement>>('chartCanvas');
  private readonly timeFormatter = new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    hourCycle: 'h23',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'UTC',
  });

  private chart: Chart<'line'> | null = null;

  /** Readings displayed in chronological order. */
  readonly readings = input.required<readonly TelemetryReading[]>();

  /** Device that owns the displayed readings. */
  readonly selectedDeviceId = input<string | null>(null);

  /** Indicates whether historical readings are being loaded. */
  readonly isLoading = input(false);

  /** User-facing historical request error. */
  readonly errorMessage = input<string | null>(null);

  /** Most recent reading displayed by the chart. */
  protected readonly latestReading = computed(() => this.readings().at(-1) ?? null);

  /** Indicates whether the chart has at least one sample. */
  protected readonly hasReadings = computed(() => this.readings().length > 0);

  private readonly chartEffect = effect(() => {
    const canvas = this.chartCanvas();
    const readings = this.readings();

    if (!canvas) {
      return;
    }

    if (!this.chart) {
      this.chart = new Chart(canvas.nativeElement, this.createConfiguration(readings));
      return;
    }

    this.updateChart(readings);
  });

  /** Releases the canvas and Chart.js event listeners. */
  ngOnDestroy(): void {
    this.chartEffect.destroy();
    this.chart?.destroy();
  }

  private createConfiguration(readings: readonly TelemetryReading[]): ChartConfiguration<'line'> {
    return {
      type: 'line',
      data: this.createChartData(readings),
      options: {
        animation: false,
        interaction: {
          intersect: false,
          mode: 'index',
        },
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: chartColours.surface,
            borderColor: chartColours.border,
            borderWidth: 1,
            displayColors: true,
            padding: 12,
            titleColor: '#e7f0f6',
          },
        },
        responsive: true,
        scales: {
          x: {
            grid: { color: chartColours.border },
            ticks: {
              autoSkip: true,
              color: chartColours.muted,
              maxRotation: 0,
              maxTicksLimit: 6,
            },
          },
          pressure: {
            grid: { color: chartColours.border },
            position: 'left',
            ticks: { color: chartColours.cyan },
            title: {
              color: chartColours.cyan,
              display: true,
              text: 'Pressure (psi)',
            },
          },
          temperature: {
            grid: { drawOnChartArea: false },
            position: 'right',
            ticks: { color: chartColours.amber },
            title: {
              color: chartColours.amber,
              display: true,
              text: 'Temperature (°C)',
            },
          },
        },
      },
    };
  }

  private createChartData(readings: readonly TelemetryReading[]): ChartData<'line'> {
    const pointRadius = readings.length === 1 ? 3 : 0;

    return {
      labels: readings.map((reading) => this.formatTimestamp(reading.timestampUtc)),
      datasets: [
        {
          backgroundColor: 'rgba(50, 214, 208, 0.08)',
          borderColor: chartColours.cyan,
          borderWidth: 2,
          data: readings.map((reading) => reading.pressurePsi),
          fill: true,
          label: 'Pressure (psi)',
          pointHoverRadius: 4,
          pointRadius,
          tension: 0.25,
          yAxisID: 'pressure',
        },
        {
          borderColor: chartColours.amber,
          borderWidth: 2,
          data: readings.map((reading) => reading.temperatureCelsius),
          fill: false,
          label: 'Temperature (°C)',
          pointHoverRadius: 4,
          pointRadius,
          tension: 0.25,
          yAxisID: 'temperature',
        },
      ],
    };
  }

  private updateChart(readings: readonly TelemetryReading[]): void {
    if (!this.chart) {
      return;
    }

    const data = this.createChartData(readings);

    this.chart.data.labels = data.labels;
    this.chart.data.datasets[0].data = data.datasets[0].data;
    this.chart.data.datasets[1].data = data.datasets[1].data;
    this.chart.update('none');
  }

  private formatTimestamp(timestampUtc: string): string {
    return this.timeFormatter.format(new Date(timestampUtc));
  }
}
