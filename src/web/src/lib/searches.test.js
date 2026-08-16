import api from './api';
import * as search from './searches';

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
      data: [{ id: 'one' }],
      headers: { 'x-total-count': '1' },
    });

    await expect(search.getAll()).resolves.toEqual({
      searches: [{ id: 'one' }],
      totalCount: 1,
    });
    expect(api.get).toHaveBeenCalledWith('/searches');
  });

  it('appends pagination and returns the total count', async () => {
    expect.hasAssertions();
    api.get.mockResolvedValue({
      data: [{ id: 'two' }],
      headers: { 'x-total-count': '250' },
    });

    await expect(search.getAll({ limit: 100, offset: 100 })).resolves.toEqual({
      searches: [{ id: 'two' }],
      totalCount: 250,
    });
    expect(api.get).toHaveBeenCalledWith('/searches?offset=100&limit=100');
  });

  it('uses the response length when the total count header is absent', async () => {
    expect.hasAssertions();
    api.get.mockResolvedValue({ data: [{}, {}], headers: {} });

    await expect(search.getAll({ limit: 100, offset: 0 })).resolves.toEqual({
      searches: [{}, {}],
      totalCount: 2,
    });
  });
});

describe('filterResponse', () => {
  it('removes VBR files if "iscbr" is specified', () => {
    const response = {
      files: [
        { bitRate: 123, isVariableBitRate: true },
        { bitRate: 320, isVariableBitRate: false },
      ],
    };

    const filters = { isCBR: true };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ bitRate: 320, isVariableBitRate: false }],
    });
  });

  it('removes CBR files if "isvbr" is specified', () => {
    const response = {
      files: [
        { bitRate: 123, isVariableBitRate: true },
        { bitRate: 320, isVariableBitRate: false },
      ],
    };

    const filters = { isVBR: true };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ bitRate: 123, isVariableBitRate: true }],
    });
  });

  it('removes all files if "iscbr" and "isvbr" are both specified', () => {
    const response = {
      files: [{ isVariableBitrate: true }, { isVariableBitrate: false }],
    };

    const filters = { isCBR: true, isVBR: true };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [],
    });
  });

  it('removes lossy files if "islossless" is specified', () => {
    const response = {
      files: [
        { bitDepth: 16, sampleRate: 41_000 },
        { bitRate: 320, isVariableBitRate: false },
      ],
    };

    const filters = { isLossless: true };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ bitDepth: 16, sampleRate: 41_000 }],
    });
  });

  it('removes lossless files if "islossy" is specified', () => {
    const response = {
      files: [
        { bitDepth: 16, sampleRate: 41_000 },
        { bitRate: 320, isVariableBitRate: false },
      ],
    };

    const filters = { isLossy: true };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ bitRate: 320, isVariableBitRate: false }],
    });
  });

  it('removes files with bitRate less than minBitRate', () => {
    const response = {
      files: [{ bitRate: 100 }, { bitRate: 99 }],
    };

    const filters = { minBitRate: 100 };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ bitRate: 100 }],
    });
  });

  it('removes files with size less than minFileSize', () => {
    const response = {
      files: [{ size: 100 }, { size: 99 }],
    };

    const filters = { minFileSize: 100 };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ size: 100 }],
    });
  });

  it('removes files with length less than minLength', () => {
    const response = {
      files: [{ length: 100 }, { length: 99 }],
    };

    const filters = { minLength: 100 };

    expect(search.filterResponse({ filters, response })).toMatchObject({
      files: [{ length: 100 }],
    });
  });

  describe('term filtering', () => {
    const response = {
      files: [
        { filename: '/path/to/foo.mp3' },
        { filename: '/path/to/bar.mp3' },
        { filename: '/path/to/baz.mp3' },
        { filename: '/path/to/qux.mp3' },
        { filename: '/path/to/info.nfo' },
        { filename: '/path/to/folder.jpg' },
      ],
    };

    it('removes files with filenames not containing included phrases', () => {
      const filters = { include: ['path', 'to', '.nfo'] };

      expect(search.filterResponse({ filters, response })).toMatchObject({
        files: [{ filename: '/path/to/info.nfo' }],
      });
    });

    it('removes files with filenames containing excluded phrases', () => {
      const filters = { exclude: ['bar', 'jpg', 'qux'] };

      expect(search.filterResponse({ filters, response })).toMatchObject({
        files: [
          { filename: '/path/to/foo.mp3' },
          { filename: '/path/to/baz.mp3' },
          { filename: '/path/to/info.nfo' },
        ],
      });
    });

    it('removes a mix of includes and excludes', () => {
      const filters = {
        exclude: ['foo', 'bar'],
        include: ['path', '.mp3'],
      };

      expect(search.filterResponse({ filters, response })).toMatchObject({
        files: [
          { filename: '/path/to/baz.mp3' },
          { filename: '/path/to/qux.mp3' },
        ],
      });
    });
  });
});

describe('parseFiltersFromString', () => {
  it('returns correct minBitrate', () => {
    expect(search.parseFiltersFromString('foo minbr:42 bar')).toMatchObject({
      minBitRate: 42,
    });

    expect(
      search.parseFiltersFromString('foo minbitrate:123 bar'),
    ).toMatchObject({
      minBitRate: 123,
    });
  });

  it('returns correct minFileSize', () => {
    expect(search.parseFiltersFromString('foo minfs:42 bar')).toMatchObject({
      minFileSize: 42,
    });

    expect(
      search.parseFiltersFromString('foo minfilesize:123 bar'),
    ).toMatchObject({
      minFileSize: 123,
    });
  });

  it('returns correct minLength', () => {
    expect(search.parseFiltersFromString('foo minlen:42 bar')).toMatchObject({
      minLength: 42,
    });

    expect(
      search.parseFiltersFromString('foo minlength:123 bar'),
    ).toMatchObject({
      minLength: 123,
    });
  });

  it('returns correct minFilesInFolder', () => {
    expect(search.parseFiltersFromString('foo minfif:42 bar')).toMatchObject({
      minFilesInFolder: 42,
    });

    expect(
      search.parseFiltersFromString('foo minfilesinfolder:123 bar'),
    ).toMatchObject({
      minFilesInFolder: 123,
    });
  });

  it('returns correct list of terms', () => {
    expect(search.parseFiltersFromString('foo minbr:42 bar')).toMatchObject({
      include: ['foo', 'bar'],
    });

    expect(search.parseFiltersFromString('foo iscbr isvbr bar')).toMatchObject({
      include: ['foo', 'bar'],
    });

    expect(search.parseFiltersFromString('foo some:thing bar')).toMatchObject({
      include: ['foo', 'bar'],
    });

    expect(search.parseFiltersFromString('foo -bar')).toMatchObject({
      exclude: ['bar'],
      include: ['foo'],
    });

    expect(search.parseFiltersFromString('-foo -bar -baz qux')).toMatchObject({
      exclude: ['foo', 'bar', 'baz'],
      include: ['qux'],
    });

    expect(search.parseFiltersFromString('foo bar baz -qux')).toMatchObject({
      exclude: ['qux'],
      include: ['foo', 'bar', 'baz'],
    });
  });

  it('returns isVBR and isCBR if terms are present', () => {
    expect(search.parseFiltersFromString('isvbr')).toMatchObject({
      isVBR: true,
    });

    expect(search.parseFiltersFromString('iscbr')).toMatchObject({
      isCBR: true,
    });
  });

  it('returns expected filters given a bit of everything', () => {
    expect(
      search.parseFiltersFromString(
        'big -mix of:everything isvbr iscbr minbr:42',
      ),
    ).toMatchObject({
      exclude: ['mix'],
      include: ['big'],
      isCBR: true,
      isVBR: true,
      minBitRate: 42,
    });
  });
});
