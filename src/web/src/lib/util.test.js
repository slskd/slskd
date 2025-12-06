import * as utils from './util';

describe('formatBytesAsUnit', () => {
  it('converts bytes to specified unit', () => {
    expect(utils.formatBytesAsUnit(1_234_567, 'MB', 2)).toBe(1.18);
  });
});

describe('formatSpeed', () => {
  it('formats bytes per second with /s suffix', () => {
    expect(utils.formatSpeed(1_048_576)).toBe('1 MB/s');
  });

  it('returns empty string for zero speed', () => {
    expect(utils.formatSpeed(0)).toBe('');
  });

  it('returns empty string for undefined speed', () => {
    expect(utils.formatSpeed(undefined)).toBe('');
  });

  it('formats small speeds in KB/s', () => {
    expect(utils.formatSpeed(10_240)).toBe('10 KB/s');
  });
});

describe('formatRemainingTime', () => {
  it('formats hours and minutes', () => {
    expect(utils.formatRemainingTime(3_661_000)).toBe('1h 01m');
  });

  it('formats minutes and seconds', () => {
    expect(utils.formatRemainingTime(125_000)).toBe('2m 5s');
  });

  it('formats only seconds', () => {
    expect(utils.formatRemainingTime(45_000)).toBe('45s');
  });

  it('returns empty string for zero time', () => {
    expect(utils.formatRemainingTime(0)).toBe('');
  });

  it('returns empty string for undefined time', () => {
    expect(utils.formatRemainingTime(undefined)).toBe('');
  });

  it('returns empty string for negative time', () => {
    expect(utils.formatRemainingTime(-1000)).toBe('');
  });
});
