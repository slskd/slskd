import api from './api';
import * as transfers from './transfers';

jest.mock('./api', () => ({
  __esModule: true,
  default: { get: jest.fn() },
}));

describe('getAll', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  it('keeps the unpaged request compatible', async () => {
    expect.hasAssertions();
    api.get.mockResolvedValue({
      data: [{ username: 'alice' }],
      headers: { 'x-total-count': '1' },
    });

    await expect(transfers.getAll({ direction: 'download' })).resolves.toEqual({
      totalCount: 1,
      transfers: [{ username: 'alice' }],
    });
    expect(api.get).toHaveBeenCalledWith('/transfers/downloads');
  });

  it('appends pagination and returns the user-group count', async () => {
    expect.hasAssertions();
    api.get.mockResolvedValue({
      data: [{ username: 'bob' }],
      headers: { 'x-total-count': '205' },
    });

    await expect(
      transfers.getAll({ direction: 'upload', limit: 100, offset: 100 }),
    ).resolves.toEqual({
      totalCount: 205,
      transfers: [{ username: 'bob' }],
    });
    expect(api.get).toHaveBeenCalledWith(
      '/transfers/uploads?offset=100&limit=100',
    );
  });

  it('uses the response length when the total count header is absent', async () => {
    expect.hasAssertions();
    api.get.mockResolvedValue({
      data: [{ username: 'alice' }, { username: 'bob' }],
      headers: {},
    });

    await expect(
      transfers.getAll({ direction: 'download', limit: 100, offset: 0 }),
    ).resolves.toMatchObject({ totalCount: 2 });
  });
});
