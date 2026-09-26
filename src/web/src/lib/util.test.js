import * as utils from './util';

describe('formatBytesAsUnit', () => {
  it('converts bytes to specified unit', () => {
    expect(utils.formatBytesAsUnit(1_234_567, 'MB', 2)).toBe(1.18);
  });
});

describe('formatBytes', () => {
  it('returns 0 B for values under one byte', () => {
    expect(utils.formatBytes(0.5)).toBe('0 B');
  });

  it('formats byte values for one and above', () => {
    expect(utils.formatBytes(1)).toBe('1 B');
    expect(utils.formatBytes(1_024)).toBe('1 KB');
  });
});

describe('getErrorMessage', () => {
  it('returns string response bodies as-is', () => {
    expect(utils.getErrorMessage({ response: { data: 'bad request' } })).toBe(
      'bad request',
    );
  });

  it('extracts detail and status from object response bodies', () => {
    const error = {
      message: 'Request failed with status code 502',
      response: {
        data: {
          detail: 'The origin web server is unreachable',
          status: 502,
          title: 'Bad gateway',
          type: 'https://developers.cloudflare.com/',
        },
      },
    };

    expect(utils.getErrorMessage(error)).toBe(
      'The origin web server is unreachable (502)',
    );
  });

  it('joins validation errors', () => {
    const error = {
      response: {
        data: {
          errors: { SearchText: ['Search text is required.'], Id: ['Bad id.'] },
          title: 'One or more validation errors occurred.',
        },
      },
    };

    expect(utils.getErrorMessage(error)).toBe(
      'Search text is required. Bad id.',
    );
  });

  it('falls back to the error message when the body has nothing usable', () => {
    const error = { message: 'Network Error', response: { data: {} } };

    expect(utils.getErrorMessage(error)).toBe('Network Error');
  });

  it('handles strings and missing errors', () => {
    expect(utils.getErrorMessage('oops')).toBe('oops');
    expect(utils.getErrorMessage(undefined, 'fallback')).toBe('fallback');
  });
});
