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
import { Chart, LinearScale, LineController, LineElement, PointElement, Tooltip } from 'chart.js';
import type { ChartConfiguration, ChartData } from 'chart.js';

import { DrillingOperation } from '../data-access/simulation-settings';
import type { TelemetryReading } from '../data-access/telemetry-reading';

Chart.register(LinearScale, LineController, LineElement, PointElement, Tooltip);

const chartColours = {
  border: 'rgba(143, 163, 181, 0.12)',
  cyan: '#32d6d0',
  muted: '#8fa3b5',
  surface: '#0c1721',
} as const;

interface DepthAxisRange {
  readonly min: number | undefined;
  readonly max: number | undefined;
}

/** Displays the selected device measured-depth trend and current operation. */
@Component({
  selector: 'app-operational-depth-chart',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './operational-depth-chart.html',
  styleUrl: './operational-depth-chart.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OperationalDepthChart implements OnDestroy {
  private readonly chartCanvas = viewChild<ElementRef<HTMLCanvasElement>>('chartCanvas');
  private readonly depthRateFormatter = new Intl.NumberFormat('en-GB', {
    maximumFractionDigits: 1,
    minimumFractionDigits: 1,
  });
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

  /** Current operation from the latest available reading. */
  readonly operation = input<DrillingOperation | null>(null);

  /** Human-readable label for the current operation. */
  readonly operationLabel = input('—');

  /** Operation values used by the template state indicator. */
  protected readonly operationType = DrillingOperation;

  /** Most recent reading displayed by the chart. */
  protected readonly latestReading = computed(() => this.readings().at(-1) ?? null);

  /** Indicates whether the chart has at least one sample. */
  protected readonly hasReadings = computed(() => this.readings().length > 0);

  /** Signed depth-change rate formatted for the current reading. */
  protected readonly latestDepthRateLabel = computed(() => {
    const rate = this.latestReading()?.depthChangeRateMetresPerHour;

    return rate === undefined ? '—' : this.formatDepthRate(rate);
  });

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
    const depthAxisRange = this.getDepthAxisRange(readings);

    return {
      type: 'line',
      data: this.createChartData(readings),
      options: {
        animation: false,
        interaction: {
          intersect: false,
          mode: 'nearest',
        },
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: chartColours.surface,
            borderColor: chartColours.border,
            borderWidth: 1,
            callbacks: {
              label: (context) => {
                const depth = context.parsed.y;

                return typeof depth === 'number'
                  ? `Measured depth: ${depth.toFixed(1)} m`
                  : 'Measured depth: —';
              },
              title: (contexts) => {
                const timestamp = contexts.at(0)?.parsed.x;

                return typeof timestamp === 'number' ? this.formatEpochTimestamp(timestamp) : '';
              },
            },
            displayColors: false,
            padding: 12,
            titleColor: '#e7f0f6',
          },
        },
        responsive: true,
        scales: {
          x: {
            grid: { color: chartColours.border },
            title: {
              color: chartColours.muted,
              display: true,
              text: 'Time (UTC)',
            },
            ticks: {
              autoSkip: true,
              callback: (value) => this.formatTickValue(value),
              color: chartColours.muted,
              maxRotation: 0,
              maxTicksLimit: 6,
            },
            type: 'linear',
          },
          y: {
            beginAtZero: false,
            grid: { color: chartColours.border },
            max: depthAxisRange.max,
            min: depthAxisRange.min,
            ticks: {
              callback: (value) => `${value} m`,
              color: chartColours.cyan,
              maxTicksLimit: 6,
            },
            title: {
              color: chartColours.cyan,
              display: true,
              text: 'Measured depth (m)',
            },
          },
        },
      },
    };
  }

  private createChartData(readings: readonly TelemetryReading[]): ChartData<'line'> {
    const pointRadius = readings.length === 1 ? 3 : 0;

    return {
      datasets: [
        {
          borderColor: chartColours.cyan,
          borderWidth: 2,
          data: readings.map((reading) => ({
            x: Date.parse(reading.timestampUtc),
            y: reading.measuredDepthMetres,
          })),
          label: 'Measured depth (m)',
          parsing: false,
          pointHoverRadius: 4,
          pointRadius,
          tension: 0,
        },
      ],
    };
  }

  private updateChart(readings: readonly TelemetryReading[]): void {
    if (!this.chart) {
      return;
    }

    const data = this.createChartData(readings);
    const depthScale = this.chart.options.scales?.['y'];
    const depthAxisRange = this.getDepthAxisRange(readings);

    this.chart.data.datasets[0].data = data.datasets[0].data;

    if (depthScale) {
      depthScale.min = depthAxisRange.min;
      depthScale.max = depthAxisRange.max;
    }

    this.chart.data.datasets[0].pointRadius = readings.length === 1 ? 3 : 0;
    this.chart.update('none');
  }

  private getDepthAxisRange(readings: readonly TelemetryReading[]): DepthAxisRange {
    if (readings.length === 0) {
      return { max: undefined, min: undefined };
    }

    let minimum = readings[0].measuredDepthMetres;
    let maximum = minimum;

    for (const reading of readings.slice(1)) {
      minimum = Math.min(minimum, reading.measuredDepthMetres);
      maximum = Math.max(maximum, reading.measuredDepthMetres);
    }

    const padding = Math.max((maximum - minimum) * 0.08, 1);

    return {
      max: maximum + padding,
      min: Math.max(0, minimum - padding),
    };
  }

  private formatDepthRate(rate: number): string {
    const formattedRate = this.depthRateFormatter.format(rate);

    return rate > 0 ? `+${formattedRate}` : formattedRate;
  }

  private formatEpochTimestamp(timestampMilliseconds: number): string {
    return this.timeFormatter.format(timestampMilliseconds);
  }

  private formatTickValue(value: string | number): string {
    const timestampMilliseconds = Number(value);

    return Number.isFinite(timestampMilliseconds)
      ? this.formatEpochTimestamp(timestampMilliseconds)
      : '';
  }
}
