import { describe, it, expect } from 'vitest';
import sharp from 'sharp';
import { renderChart } from '../src/services/chart.service.js';
import { buildApp } from '../src/app.js';

describe('Chart Rendering Service & Routes', () => {
  it('renders a vertical bar chart card in 16:9 ratio', async () => {
    const buffer = await renderChart({
      type: 'bar',
      title: 'Quarterly Revenue Growth',
      subtitle: 'Values in Millions USD (2026)',
      labels: ['Q1', 'Q2', 'Q3', 'Q4'],
      datasets: [
        {
          label: 'Revenue',
          data: [12.4, 18.2, 24.8, 31.5],
        },
      ],
      theme: 'dark',
      aspectRatio: '16:9',
      cardStyle: true,
    });

    expect(buffer).toBeInstanceOf(Buffer);
    expect(buffer.length).toBeGreaterThan(1000);

    const metadata = await sharp(buffer).metadata();
    expect(metadata.format).toBe('png');
    expect(metadata.width).toBe(1200);
    expect(metadata.height).toBe(675);
  });

  it('renders a horizontal bar chart in 1:1 ratio with multiple datasets', async () => {
    const buffer = await renderChart({
      type: 'horizontal_bar',
      title: 'Platform Engagement',
      labels: ['Bluesky', 'Twitter/X', 'LinkedIn', 'Threads'],
      datasets: [
        {
          label: 'Shares',
          data: [420, 890, 310, 650],
          backgroundColor: '#6366f1',
        },
        {
          label: 'Likes',
          data: [1250, 3100, 890, 2200],
          backgroundColor: '#10b981',
        },
      ],
      theme: 'light',
      aspectRatio: '1:1',
      cardStyle: true,
    });

    const metadata = await sharp(buffer).metadata();
    expect(metadata.format).toBe('png');
    expect(metadata.width).toBe(1080);
    expect(metadata.height).toBe(1080);
  });

  it('renders line and area charts with 4:5 portrait ratio', async () => {
    const buffer = await renderChart({
      type: 'area',
      title: 'Monthly Active Followers',
      labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May'],
      datasets: [
        {
          label: 'Organic Followers',
          data: [1200, 1900, 3000, 5200, 7800],
          borderColor: '#f59e0b',
        },
      ],
      theme: 'dark',
      aspectRatio: '4:5',
      cardStyle: true,
    });

    const metadata = await sharp(buffer).metadata();
    expect(metadata.width).toBe(1080);
    expect(metadata.height).toBe(1350);
  });

  it('renders a doughnut chart with automatic palette', async () => {
    const buffer = await renderChart({
      type: 'doughnut',
      title: 'Traffic Sources',
      labels: ['Search', 'Direct', 'Referral', 'Social'],
      datasets: [
        {
          data: [45, 25, 20, 10],
        },
      ],
      theme: 'dark',
      aspectRatio: '1:1',
      cardStyle: true,
    });

    const metadata = await sharp(buffer).metadata();
    expect(metadata.width).toBe(1080);
    expect(metadata.height).toBe(1080);
  });

  it('renders bare transparent canvas when cardStyle is false', async () => {
    const buffer = await renderChart({
      type: 'line',
      labels: ['A', 'B', 'C'],
      datasets: [
        {
          label: 'Metric',
          data: [10, 20, 15],
        },
      ],
      theme: 'dark',
      aspectRatio: '16:9',
      cardStyle: false,
    });

    expect(buffer).toBeInstanceOf(Buffer);
    const metadata = await sharp(buffer).metadata();
    expect(metadata.hasAlpha).toBe(true);
  });

  it('POST /render/chart returns 200 with PNG image stream and dimension headers', async () => {
    const app = buildApp();
    const response = await app.inject({
      method: 'POST',
      url: '/render/chart',
      payload: {
        type: 'bar',
        title: 'Weekly Commits',
        labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'],
        datasets: [
          {
            data: [5, 12, 8, 14, 9],
          },
        ],
        theme: 'dark',
        aspectRatio: '16:9',
        cardStyle: true,
      },
    });

    expect(response.statusCode).toBe(200);
    expect(response.headers['content-type']).toBe('image/png');
    expect(response.headers['x-width']).toBe('1200');
    expect(response.headers['x-height']).toBe('675');
    expect(response.rawPayload.length).toBeGreaterThan(1000);
  });

  it('POST /render/chart returns 400 for invalid chart payloads', async () => {
    const app = buildApp();
    const response = await app.inject({
      method: 'POST',
      url: '/render/chart',
      payload: {
        type: 'unknown_chart_type',
        labels: [],
        datasets: [],
      },
    });

    expect(response.statusCode).toBe(400);
    const body = JSON.parse(response.payload);
    expect(body.error).toBe('Chart validation failed');
  });
});
