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

import type { TelemetryReading } from '../data-access/telemetry-reading';

Chart.register(LinearScale, LineController, LineElement, PointElement, Tooltip);

const chartColours = {
  amber: '#f5ae51',
  border: 'rgba(143, 163, 181, 0.12)',
  muted: '#8fa3b5',
  surface: '#0c1721',
} as const;

const minimumDepthAxisPaddingMetres = 0.05;

interface DepthAxisRange {
  readonly min: number | undefined;
  readonly max: number | undefined;
}

interface GammaRayAxisRange {
  readonly min: number | undefined;
  readonly max: number | undefined;
}

/** Displays the selected device gamma ray curve by measured depth. */
@Component({
  selector: 'app-gamma-ray-chart',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './gamma-ray-chart.html',
  styleUrl: './gamma-ray-chart.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GammaRayChart implements OnDestroy {
  private readonly chartCanvas = viewChild<ElementRef<HTMLCanvasElement>>('chartCanvas');
  private readonly gammaRayFormatter = new Intl.NumberFormat('en-GB', {
    maximumFractionDigits: 1,
    minimumFractionDigits: 1,
  });
  private readonly depthFormatter = new Intl.NumberFormat('en-GB', {
    maximumFractionDigits: 2,
    minimumFractionDigits: 1,
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
    const depthAxisRange = this.getDepthAxisRange(readings);
    const gammaRayAxisRange = this.getGammaRayAxisRange(readings);

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
                const gammaRay = context.parsed.x;

                return typeof gammaRay === 'number'
                  ? `Gamma ray: ${this.formatGammaRay(gammaRay)} gAPI`
                  : 'Gamma ray: —';
              },
              title: (contexts) => {
                const depth = contexts.at(0)?.parsed.y;

                return typeof depth === 'number' ? `Depth: ${this.formatDepth(depth)} m` : '';
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
            beginAtZero: false,
            grid: { color: chartColours.border },
            max: gammaRayAxisRange.max,
            min: gammaRayAxisRange.min,
            ticks: {
              callback: (value) => this.formatGammaRay(Number(value)),
              color: chartColours.amber,
              maxTicksLimit: 6,
            },
            title: {
              color: chartColours.amber,
              display: true,
              text: 'Gamma ray (gAPI)',
            },
            type: 'linear',
          },
          y: {
            beginAtZero: false,
            grid: { color: chartColours.border },
            max: depthAxisRange.max,
            min: depthAxisRange.min,
            reverse: true,
            ticks: {
              callback: (value) => `${this.formatDepth(Number(value))} m`,
              color: chartColours.muted,
              maxTicksLimit: 8,
            },
            title: {
              color: chartColours.muted,
              display: true,
              text: 'Measured depth (m)',
            },
            type: 'linear',
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
          borderColor: chartColours.amber,
          borderWidth: 2,
          data: readings.map((reading) => ({
            x: reading.gammaRayApi,
            y: reading.measuredDepthMetres,
          })),
          label: 'Gamma ray (gAPI)',
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
    const gammaRayScale = this.chart.options.scales?.['x'];
    const depthAxisRange = this.getDepthAxisRange(readings);
    const gammaRayAxisRange = this.getGammaRayAxisRange(readings);

    this.chart.data.datasets[0].data = data.datasets[0].data;

    if (depthScale) {
      depthScale.min = depthAxisRange.min;
      depthScale.max = depthAxisRange.max;
    }

    if (gammaRayScale) {
      gammaRayScale.min = gammaRayAxisRange.min;
      gammaRayScale.max = gammaRayAxisRange.max;
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

    const padding = Math.max((maximum - minimum) * 0.08, minimumDepthAxisPaddingMetres);

    return {
      max: maximum + padding,
      min: minimum - padding,
    };
  }

  private getGammaRayAxisRange(readings: readonly TelemetryReading[]): GammaRayAxisRange {
    if (readings.length === 0) {
      return { max: undefined, min: undefined };
    }

    let minimum = readings[0].gammaRayApi;
    let maximum = minimum;

    for (const reading of readings.slice(1)) {
      minimum = Math.min(minimum, reading.gammaRayApi);
      maximum = Math.max(maximum, reading.gammaRayApi);
    }

    const padding = Math.max((maximum - minimum) * 0.08, 1);

    return {
      max: maximum + padding,
      min: Math.max(0, minimum - padding),
    };
  }

  private formatGammaRay(value: number): string {
    return this.gammaRayFormatter.format(value);
  }

  private formatDepth(depth: number): string {
    return this.depthFormatter.format(depth);
  }
}
