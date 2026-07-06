import api from './api';
import * as reports from './reports';

jest.mock('./api', () => ({
  __esModule: true,
  default: { get: jest.fn() },
}));

describe('reports', () => {
  beforeEach(() => {
    api.get.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  const urlOf = () => api.get.mock.calls[0][0];
  const paramsOf = () => new URLSearchParams(urlOf().split('?')[1] ?? '');

  describe('getSummary', () => {
    it('calls the correct endpoint', async () => {
      await reports.getSummary();
      expect(urlOf()).toContain('/telemetry/reports/transfers/summary');
    });

    it('appends start as an ISO string', async () => {
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getSummary({ start });
      expect(paramsOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getSummary({ end });
      expect(paramsOf().get('end')).toBe(end.toISOString());
    });

    it('appends direction when provided', async () => {
      await reports.getSummary({ direction: 'Upload' });
      expect(paramsOf().get('direction')).toBe('Upload');
    });

    it('appends username when provided', async () => {
      await reports.getSummary({ username: 'testuser' });
      expect(paramsOf().get('username')).toBe('testuser');
    });

    it('omits optional params when not provided', async () => {
      await reports.getSummary();
      const p = paramsOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
      expect(p.has('direction')).toBe(false);
      expect(p.has('username')).toBe(false);
    });
  });

  describe('getHistogram', () => {
    it('calls the correct endpoint', async () => {
      await reports.getHistogram();
      expect(urlOf()).toContain('/telemetry/reports/transfers/histogram');
    });

    it('appends start as an ISO string', async () => {
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getHistogram({ start });
      expect(paramsOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getHistogram({ end });
      expect(paramsOf().get('end')).toBe(end.toISOString());
    });

    it('appends buckets when provided', async () => {
      await reports.getHistogram({ buckets: 50 });
      expect(paramsOf().get('buckets')).toBe('50');
    });

    it('appends direction when provided', async () => {
      await reports.getHistogram({ direction: 'Download' });
      expect(paramsOf().get('direction')).toBe('Download');
    });

    it('appends username when provided', async () => {
      await reports.getHistogram({ username: 'testuser' });
      expect(paramsOf().get('username')).toBe('testuser');
    });

    it('omits optional params when not provided', async () => {
      await reports.getHistogram();
      const p = paramsOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
      expect(p.has('buckets')).toBe(false);
      expect(p.has('direction')).toBe(false);
      expect(p.has('username')).toBe(false);
    });
  });

  describe('getLeaderboard', () => {
    it('calls the correct endpoint', async () => {
      await reports.getLeaderboard();
      expect(urlOf()).toContain('/telemetry/reports/transfers/leaderboard');
    });

    it('appends direction when provided', async () => {
      await reports.getLeaderboard({ direction: 'Upload' });
      expect(paramsOf().get('direction')).toBe('Upload');
    });

    it('defaults limit to 10', async () => {
      await reports.getLeaderboard();
      expect(paramsOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      await reports.getLeaderboard({ limit: 25 });
      expect(paramsOf().get('limit')).toBe('25');
    });

    it('defaults sortBy to Count', async () => {
      await reports.getLeaderboard();
      expect(paramsOf().get('sortBy')).toBe('Count');
    });

    it('appends the specified sortBy', async () => {
      await reports.getLeaderboard({ sortBy: 'TotalBytes' });
      expect(paramsOf().get('sortBy')).toBe('TotalBytes');
    });

    it('appends start as an ISO string', async () => {
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getLeaderboard({ start });
      expect(paramsOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getLeaderboard({ end });
      expect(paramsOf().get('end')).toBe(end.toISOString());
    });

    it('omits start and end when not provided', async () => {
      await reports.getLeaderboard();
      const p = paramsOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });

  describe('getTopDirectories', () => {
    it('calls the correct endpoint', async () => {
      await reports.getTopDirectories();
      expect(urlOf()).toContain('/telemetry/reports/transfers/directories');
    });

    it('defaults limit to 10', async () => {
      await reports.getTopDirectories();
      expect(paramsOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      await reports.getTopDirectories({ limit: 20 });
      expect(paramsOf().get('limit')).toBe('20');
    });

    it('appends start as an ISO string', async () => {
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getTopDirectories({ start });
      expect(paramsOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getTopDirectories({ end });
      expect(paramsOf().get('end')).toBe(end.toISOString());
    });

    it('omits start and end when not provided', async () => {
      await reports.getTopDirectories();
      const p = paramsOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });

  describe('getExceptionPareto', () => {
    it('calls the correct endpoint', async () => {
      await reports.getExceptionPareto();
      expect(urlOf()).toContain('/telemetry/reports/transfers/exceptions/pareto');
    });

    it('appends direction when provided', async () => {
      await reports.getExceptionPareto({ direction: 'Download' });
      expect(paramsOf().get('direction')).toBe('Download');
    });

    it('defaults limit to 10', async () => {
      await reports.getExceptionPareto();
      expect(paramsOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      await reports.getExceptionPareto({ limit: 20 });
      expect(paramsOf().get('limit')).toBe('20');
    });

    it('appends start as an ISO string', async () => {
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getExceptionPareto({ start });
      expect(paramsOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getExceptionPareto({ end });
      expect(paramsOf().get('end')).toBe(end.toISOString());
    });

    it('omits direction, start, and end when not provided', async () => {
      await reports.getExceptionPareto();
      const p = paramsOf();
      expect(p.has('direction')).toBe(false);
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });

  describe('getExceptions', () => {
    it('calls the correct endpoint', async () => {
      await reports.getExceptions();
      expect(urlOf()).toContain('/telemetry/reports/transfers/exceptions');
      expect(urlOf()).not.toContain('/exceptions/pareto');
    });

    it('appends direction when provided', async () => {
      await reports.getExceptions({ direction: 'Upload' });
      expect(paramsOf().get('direction')).toBe('Upload');
    });

    it('defaults limit to 10', async () => {
      await reports.getExceptions();
      expect(paramsOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      await reports.getExceptions({ limit: 50 });
      expect(paramsOf().get('limit')).toBe('50');
    });

    it('defaults sortOrder to DESC', async () => {
      await reports.getExceptions();
      expect(paramsOf().get('sortOrder')).toBe('DESC');
    });

    it('appends the specified sortOrder', async () => {
      await reports.getExceptions({ sortOrder: 'ASC' });
      expect(paramsOf().get('sortOrder')).toBe('ASC');
    });

    it('appends start as an ISO string', async () => {
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getExceptions({ start });
      expect(paramsOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getExceptions({ end });
      expect(paramsOf().get('end')).toBe(end.toISOString());
    });

    it('omits direction, start, and end when not provided', async () => {
      await reports.getExceptions();
      const p = paramsOf();
      expect(p.has('direction')).toBe(false);
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });
});
