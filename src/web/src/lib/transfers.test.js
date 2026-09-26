import api from './api';
import { retryDownloads } from './transfers';

jest.mock('./api', () => ({ post: jest.fn() }));

const tooManyRequests = () =>
  Object.assign(new Error('Request failed with status code 429'), {
    response: { status: 429 },
  });

describe('retryDownloads', () => {
  beforeEach(() => {
    api.post.mockReset();
  });

  it('sends one request per user with all of their files', async () => {
    expect.assertions(3);

    api.post.mockResolvedValue({});

    await retryDownloads([
      { filename: 'a', size: 1, username: 'alice' },
      { filename: 'b', size: 2, username: 'bob' },
      { filename: 'c', size: 3, username: 'alice' },
    ]);

    expect(api.post).toHaveBeenCalledTimes(2);
    expect(api.post).toHaveBeenCalledWith('/transfers/downloads/alice', [
      { filename: 'a', size: 1 },
      { filename: 'c', size: 3 },
    ]);
    expect(api.post).toHaveBeenCalledWith('/transfers/downloads/bob', [
      { filename: 'b', size: 2 },
    ]);
  });

  it('never has more than two requests in flight', async () => {
    expect.assertions(2);

    let inFlight = 0;
    let peak = 0;

    api.post.mockImplementation(async () => {
      inFlight++;
      peak = Math.max(peak, inFlight);
      await new Promise((resolve) => {
        setTimeout(resolve, 5);
      });
      inFlight--;
      return {};
    });

    const files = Array.from({ length: 10 }, (_, index) => ({
      filename: `f${index}`,
      size: index,
      username: `user${index}`,
    }));

    await retryDownloads(files);

    expect(api.post).toHaveBeenCalledTimes(10);
    expect(peak).toBe(2);
  });

  it('retries a user after a 429', async () => {
    expect.assertions(2);

    api.post.mockRejectedValueOnce(tooManyRequests()).mockResolvedValue({});

    const { failed } = await retryDownloads(
      [{ filename: 'a', size: 1, username: 'alice' }],
      { backoff: 1 },
    );

    expect(api.post).toHaveBeenCalledTimes(2);
    expect(failed).toEqual([]);
  });

  it('reports users that fail, without retrying other errors', async () => {
    expect.assertions(2);

    const offline = Object.assign(new Error('offline'), {
      response: { status: 500 },
    });

    // alice is requested first, then bob
    api.post.mockResolvedValueOnce({}).mockRejectedValueOnce(offline);

    const { failed } = await retryDownloads(
      [
        { filename: 'a', size: 1, username: 'alice' },
        { filename: 'b', size: 2, username: 'bob' },
        { filename: 'c', size: 3, username: 'bob' },
      ],
      { backoff: 1 },
    );

    expect(api.post).toHaveBeenCalledTimes(2);
    expect(failed).toEqual([{ count: 2, error: offline, username: 'bob' }]);
  });
});
