import * as utils from './util';

it('converts bytes to specified unit', () => {
  expect(utils.formatBytesAsUnit(1234567, 2, 'MB')).toBe(1.18);
});