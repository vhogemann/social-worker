import { Chart, registerables, ChartConfiguration } from 'chart.js';
import { createCanvas } from '@napi-rs/canvas';
import { initFonts } from './template.service.js';
import { ChartRenderSpec, ChartType } from '../types/chart.js';

Chart.register(...registerables);

const DEFAULT_PALETTE = [
  '#6366f1',
  '#10b981',
  '#f59e0b',
  '#f43f5e',
  '#0ea5e9',
  '#8b5cf6',
  '#14b8a6',
  '#ec4899',
];

function hexToRgba(hex: string, alpha: number): string {
  const cleanHex = hex.replace('#', '');
  if (cleanHex.length === 3) {
    const r = parseInt(cleanHex[0] + cleanHex[0], 16);
    const g = parseInt(cleanHex[1] + cleanHex[1], 16);
    const b = parseInt(cleanHex[2] + cleanHex[2], 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }
  if (cleanHex.length === 6) {
    const r = parseInt(cleanHex.substring(0, 2), 16);
    const g = parseInt(cleanHex.substring(2, 4), 16);
    const b = parseInt(cleanHex.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }
  return hex;
}

export function getResolution(aspectRatio: '16:9' | '1:1' | '4:5'): { width: number; height: number } {
  switch (aspectRatio) {
    case '1:1':
      return { width: 1080, height: 1080 };
    case '4:5':
      return { width: 1080, height: 1350 };
    case '16:9':
    default:
      return { width: 1200, height: 675 };
  }
}

export async function renderChart(spec: ChartRenderSpec): Promise<Buffer> {
  initFonts();

  const { width: totalWidth, height: totalHeight } = getResolution(spec.aspectRatio);
  const isDark = spec.theme !== 'light';

  const themeColors = isDark
    ? {
        bg: '#0f172a',
        cardBg: '#1e293b',
        border: '#334155',
        title: '#f8fafc',
        subtitle: '#94a3b8',
        text: '#94a3b8',
        grid: 'rgba(255, 255, 255, 0.08)',
        legend: '#cbd5e1',
      }
    : {
        bg: '#f8fafc',
        cardBg: '#ffffff',
        border: '#e2e8f0',
        title: '#0f172a',
        subtitle: '#64748b',
        text: '#64748b',
        grid: 'rgba(0, 0, 0, 0.06)',
        legend: '#334155',
      };

  let chartW = totalWidth;
  let chartH = totalHeight;
  let chartX = 0;
  let chartY = 0;

  let mainCanvas = createCanvas(totalWidth, totalHeight);
  let mainCtx = mainCanvas.getContext('2d');

  if (spec.cardStyle) {
    const padding = 48;
    const innerPadding = 32;

    mainCtx.fillStyle = themeColors.bg;
    mainCtx.fillRect(0, 0, totalWidth, totalHeight);

    const cardX = padding;
    const cardY = padding;
    const cardW = totalWidth - padding * 2;
    const cardH = totalHeight - padding * 2;
    const cardRadius = 24;

    mainCtx.save();
    mainCtx.beginPath();
    mainCtx.roundRect(cardX, cardY, cardW, cardH, cardRadius);
    mainCtx.fillStyle = themeColors.cardBg;
    mainCtx.fill();
    mainCtx.lineWidth = 1.5;
    mainCtx.strokeStyle = themeColors.border;
    mainCtx.stroke();
    mainCtx.restore();

    let headerOffset = cardY + innerPadding;
    if (spec.title) {
      mainCtx.font = 'bold 30px Inter, sans-serif';
      mainCtx.fillStyle = themeColors.title;
      mainCtx.textBaseline = 'top';
      mainCtx.fillText(spec.title, cardX + innerPadding, headerOffset);
      headerOffset += 38;
    }

    if (spec.subtitle) {
      mainCtx.font = '16px Inter, sans-serif';
      mainCtx.fillStyle = themeColors.subtitle;
      mainCtx.textBaseline = 'top';
      mainCtx.fillText(spec.subtitle, cardX + innerPadding, headerOffset);
      headerOffset += 28;
    }

    const headerHeight = headerOffset - cardY;
    chartX = cardX + innerPadding;
    chartY = cardY + headerHeight + 12;
    chartW = cardW - innerPadding * 2;
    chartH = cardH - headerHeight - innerPadding - 12;
  } else {
    mainCtx.clearRect(0, 0, totalWidth, totalHeight);
  }

  const chartCanvas = createCanvas(chartW, chartH);
  (chartCanvas as any).style = {};

  const isRadial = spec.type === 'pie' || spec.type === 'doughnut';
  const isHorizontal = spec.type === 'horizontal_bar';
  const isArea = spec.type === 'area';

  const processedDatasets = spec.datasets.map((ds, dsIdx) => {
    const defaultColor = DEFAULT_PALETTE[dsIdx % DEFAULT_PALETTE.length];

    if (isRadial) {
      const sliceColors = spec.labels.map((_, i) => DEFAULT_PALETTE[i % DEFAULT_PALETTE.length]);
      return {
        label: ds.label ?? '',
        data: ds.data,
        backgroundColor: ds.backgroundColor ?? sliceColors,
        borderColor: ds.borderColor ?? (isDark ? '#1e293b' : '#ffffff'),
        borderWidth: 2,
      };
    }

    if (spec.type === 'bar' || isHorizontal) {
      const bg = ds.backgroundColor ?? (spec.datasets.length === 1
        ? spec.labels.map((_, i) => DEFAULT_PALETTE[i % DEFAULT_PALETTE.length])
        : defaultColor);

      return {
        label: ds.label ?? '',
        data: ds.data,
        backgroundColor: bg,
        borderColor: ds.borderColor ?? bg,
        borderRadius: 8,
        borderSkipped: false,
      };
    }

    const color = (typeof ds.borderColor === 'string' ? ds.borderColor : null)
      ?? (typeof ds.backgroundColor === 'string' ? ds.backgroundColor : null)
      ?? defaultColor;

    const fillArea = isArea || ds.fill === true;
    const areaBg = typeof ds.backgroundColor === 'string'
      ? ds.backgroundColor
      : hexToRgba(color, 0.2);

    return {
      label: ds.label ?? '',
      data: ds.data,
      borderColor: color,
      backgroundColor: fillArea ? areaBg : 'transparent',
      fill: fillArea,
      tension: 0.35,
      borderWidth: 3,
      pointRadius: 4,
      pointHoverRadius: 6,
      pointBackgroundColor: color,
    };
  });

  const chartType = (isHorizontal ? 'bar' : (isArea ? 'line' : spec.type)) as any;

  const chartConfig: ChartConfiguration = {
    type: chartType,
    data: {
      labels: spec.labels,
      datasets: processedDatasets as any,
    },
    options: {
      responsive: false,
      animation: false,
      indexAxis: isHorizontal ? 'y' : 'x',
      layout: {
        padding: isRadial ? 16 : 8,
      },
      plugins: {
        legend: {
          display: isRadial || spec.datasets.some(d => Boolean(d.label)),
          position: isRadial ? 'right' : 'top',
          labels: {
            color: themeColors.legend,
            font: {
              family: 'Inter, sans-serif',
              size: 14,
            },
            padding: 16,
            usePointStyle: true,
            boxWidth: 8,
          },
        },
        tooltip: {
          enabled: false,
        },
      },
      scales: isRadial
        ? undefined
        : {
            x: {
              grid: {
                color: isHorizontal ? themeColors.grid : 'transparent',
              },
              ticks: {
                color: themeColors.text,
                font: {
                  family: 'Inter, sans-serif',
                  size: 13,
                },
                maxRotation: 45,
              },
              border: {
                color: themeColors.border,
              },
            },
            y: {
              grid: {
                color: isHorizontal ? 'transparent' : themeColors.grid,
              },
              ticks: {
                color: themeColors.text,
                font: {
                  family: 'Inter, sans-serif',
                  size: 13,
                },
              },
              border: {
                color: themeColors.border,
              },
            },
          },
    },
  };

  const chart = new Chart(chartCanvas as any, chartConfig);
  chart.render();

  if (spec.cardStyle) {
    mainCtx.drawImage(chartCanvas, chartX, chartY);
    chart.destroy();
    return mainCanvas.toBuffer('image/png');
  }

  const resultBuffer = chartCanvas.toBuffer('image/png');
  chart.destroy();
  return resultBuffer;
}
