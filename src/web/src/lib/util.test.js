import * as utils from './util';

test('converts bytes to specified unit', () => {
  expect(utils.formatBytesAsUnit(1_234_567, 2, 'MB')).toBe(1.18);
});
