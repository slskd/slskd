import api from './api';
import * as reports from './reports';

jest.mock('./api', () => ({
  __esModule: true,
  default: { get: jest.fn() },
}));

const urlOf = () => api.get.mock.calls[0][0];
const parametersOf = () => new URLSearchParams(urlOf().split('?')[1] ?? '');

describe('reports', () => {
  beforeEach(() => {
    api.get.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('getSummary', () => {
    it('calls the correct endpoint', async () => {
      expect.hasAssertions();
      await reports.getSummary();
      expect(urlOf()).toContain('/telemetry/reports/transfers/summary');
    });

    it('appends start as an ISO string', async () => {
      expect.hasAssertions();
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getSummary({ start });
      expect(parametersOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      expect.hasAssertions();
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getSummary({ end });
      expect(parametersOf().get('end')).toBe(end.toISOString());
    });

    it('appends direction when provided', async () => {
      expect.hasAssertions();
      await reports.getSummary({ direction: 'Upload' });
      expect(parametersOf().get('direction')).toBe('Upload');
    });

    it('appends username when provided', async () => {
      expect.hasAssertions();
      await reports.getSummary({ username: 'testuser' });
      expect(parametersOf().get('username')).toBe('testuser');
    });

    it('omits optional params when not provided', async () => {
      expect.hasAssertions();
      await reports.getSummary();
      const p = parametersOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
      expect(p.has('direction')).toBe(false);
      expect(p.has('username')).toBe(false);
    });
  });

  describe('getHistogram', () => {
    it('calls the correct endpoint', async () => {
      expect.hasAssertions();
      await reports.getHistogram();
      expect(urlOf()).toContain('/telemetry/reports/transfers/histogram');
    });

    it('appends start as an ISO string', async () => {
      expect.hasAssertions();
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getHistogram({ start });
      expect(parametersOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      expect.hasAssertions();
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getHistogram({ end });
      expect(parametersOf().get('end')).toBe(end.toISOString());
    });

    it('appends buckets when provided', async () => {
      expect.hasAssertions();
      await reports.getHistogram({ buckets: 50 });
      expect(parametersOf().get('buckets')).toBe('50');
    });

    it('appends direction when provided', async () => {
      expect.hasAssertions();
      await reports.getHistogram({ direction: 'Download' });
      expect(parametersOf().get('direction')).toBe('Download');
    });

    it('appends username when provided', async () => {
      expect.hasAssertions();
      await reports.getHistogram({ username: 'testuser' });
      expect(parametersOf().get('username')).toBe('testuser');
    });

    it('omits optional params when not provided', async () => {
      expect.hasAssertions();
      await reports.getHistogram();
      const p = parametersOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
      expect(p.has('buckets')).toBe(false);
      expect(p.has('direction')).toBe(false);
      expect(p.has('username')).toBe(false);
    });
  });

  describe('getLeaderboard', () => {
    it('calls the correct endpoint', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard();
      expect(urlOf()).toContain('/telemetry/reports/transfers/leaderboard');
    });

    it('appends direction when provided', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard({ direction: 'Upload' });
      expect(parametersOf().get('direction')).toBe('Upload');
    });

    it('defaults limit to 10', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard();
      expect(parametersOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard({ limit: 25 });
      expect(parametersOf().get('limit')).toBe('25');
    });

    it('defaults sortBy to Count', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard();
      expect(parametersOf().get('sortBy')).toBe('Count');
    });

    it('appends the specified sortBy', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard({ sortBy: 'TotalBytes' });
      expect(parametersOf().get('sortBy')).toBe('TotalBytes');
    });

    it('appends start as an ISO string', async () => {
      expect.hasAssertions();
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getLeaderboard({ start });
      expect(parametersOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      expect.hasAssertions();
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getLeaderboard({ end });
      expect(parametersOf().get('end')).toBe(end.toISOString());
    });

    it('omits start and end when not provided', async () => {
      expect.hasAssertions();
      await reports.getLeaderboard();
      const p = parametersOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });

  describe('getTopDirectories', () => {
    it('calls the correct endpoint', async () => {
      expect.hasAssertions();
      await reports.getTopDirectories();
      expect(urlOf()).toContain('/telemetry/reports/transfers/directories');
    });

    it('defaults limit to 10', async () => {
      expect.hasAssertions();
      await reports.getTopDirectories();
      expect(parametersOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      expect.hasAssertions();
      await reports.getTopDirectories({ limit: 20 });
      expect(parametersOf().get('limit')).toBe('20');
    });

    it('appends start as an ISO string', async () => {
      expect.hasAssertions();
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getTopDirectories({ start });
      expect(parametersOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      expect.hasAssertions();
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getTopDirectories({ end });
      expect(parametersOf().get('end')).toBe(end.toISOString());
    });

    it('omits start and end when not provided', async () => {
      expect.hasAssertions();
      await reports.getTopDirectories();
      const p = parametersOf();
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });

  describe('getExceptionPareto', () => {
    it('calls the correct endpoint', async () => {
      expect.hasAssertions();
      await reports.getExceptionPareto();
      expect(urlOf()).toContain(
        '/telemetry/reports/transfers/exceptions/pareto',
      );
    });

    it('appends direction when provided', async () => {
      expect.hasAssertions();
      await reports.getExceptionPareto({ direction: 'Download' });
      expect(parametersOf().get('direction')).toBe('Download');
    });

    it('defaults limit to 10', async () => {
      expect.hasAssertions();
      await reports.getExceptionPareto();
      expect(parametersOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      expect.hasAssertions();
      await reports.getExceptionPareto({ limit: 20 });
      expect(parametersOf().get('limit')).toBe('20');
    });

    it('appends start as an ISO string', async () => {
      expect.hasAssertions();
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getExceptionPareto({ start });
      expect(parametersOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      expect.hasAssertions();
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getExceptionPareto({ end });
      expect(parametersOf().get('end')).toBe(end.toISOString());
    });

    it('omits direction, start, and end when not provided', async () => {
      expect.hasAssertions();
      await reports.getExceptionPareto();
      const p = parametersOf();
      expect(p.has('direction')).toBe(false);
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });

  describe('getExceptions', () => {
    it('calls the correct endpoint', async () => {
      expect.hasAssertions();
      await reports.getExceptions();
      expect(urlOf()).toContain('/telemetry/reports/transfers/exceptions');
      expect(urlOf()).not.toContain('/exceptions/pareto');
    });

    it('appends direction when provided', async () => {
      expect.hasAssertions();
      await reports.getExceptions({ direction: 'Upload' });
      expect(parametersOf().get('direction')).toBe('Upload');
    });

    it('defaults limit to 10', async () => {
      expect.hasAssertions();
      await reports.getExceptions();
      expect(parametersOf().get('limit')).toBe('10');
    });

    it('appends the specified limit', async () => {
      expect.hasAssertions();
      await reports.getExceptions({ limit: 50 });
      expect(parametersOf().get('limit')).toBe('50');
    });

    it('defaults sortOrder to DESC', async () => {
      expect.hasAssertions();
      await reports.getExceptions();
      expect(parametersOf().get('sortOrder')).toBe('DESC');
    });

    it('appends the specified sortOrder', async () => {
      expect.hasAssertions();
      await reports.getExceptions({ sortOrder: 'ASC' });
      expect(parametersOf().get('sortOrder')).toBe('ASC');
    });

    it('appends start as an ISO string', async () => {
      expect.hasAssertions();
      const start = new Date('2024-01-01T00:00:00.000Z');
      await reports.getExceptions({ start });
      expect(parametersOf().get('start')).toBe(start.toISOString());
    });

    it('appends end as an ISO string', async () => {
      expect.hasAssertions();
      const end = new Date('2024-12-31T00:00:00.000Z');
      await reports.getExceptions({ end });
      expect(parametersOf().get('end')).toBe(end.toISOString());
    });

    it('omits direction, start, and end when not provided', async () => {
      expect.hasAssertions();
      await reports.getExceptions();
      const p = parametersOf();
      expect(p.has('direction')).toBe(false);
      expect(p.has('start')).toBe(false);
      expect(p.has('end')).toBe(false);
    });
  });
});
