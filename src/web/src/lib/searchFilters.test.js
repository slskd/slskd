import {
  countFilters,
  filterResponse,
  parseDuration,
  parseFiltersFromString,
  parseSize,
  serializeFilters,
} from './searchFilters';

describe('parseSize', () => {
  it('reads bare bytes and unit suffixes', () => {
    expect(parseSize('42')).toBe(42);
    expect(parseSize('2k')).toBe(2_048);
    expect(parseSize('1.5mb')).toBe(1_572_864);
    expect(parseSize('1G')).toBe(1_073_741_824);
  });

  it('rejects garbage', () => {
    expect(parseSize('big')).toBeUndefined();
    expect(parseSize('-1')).toBeUndefined();
  });
});

describe('parseDuration', () => {
  it('reads seconds, m:ss and h:mm:ss', () => {
    expect(parseDuration('90')).toBe(90);
    expect(parseDuration('3:30')).toBe(210);
    expect(parseDuration('1:02:03')).toBe(3_723);
  });

  it('rejects partial input', () => {
    expect(parseDuration('3:')).toBeUndefined();
    expect(parseDuration('a')).toBeUndefined();
  });
});

describe('parseFiltersFromString', () => {
  it('reads the new filters', () => {
    expect(
      parseFiltersFromString(
        'ext:flac,.MP3 minsr:44.1 maxfs:50mb maxlen:10:00 maxq:0 minspeed:500k',
      ),
    ).toMatchObject({
      extensions: ['flac', 'mp3'],
      maxFileSize: 52_428_800,
      maxLength: 600,
      maxQueueLength: 0,
      minSampleRate: 44_100,
      minUploadSpeed: 512_000,
    });
  });

  it('leaves maximums unset when absent', () => {
    expect(parseFiltersFromString('foo').maxQueueLength).toBeUndefined();
  });

  it('ignores filters with values that do not parse', () => {
    expect(parseFiltersFromString('minbr:lots maxq:x')).toMatchObject({
      include: [],
      maxQueueLength: undefined,
      minBitRate: 0,
    });
  });

  it('ignores extra whitespace and lone dashes', () => {
    expect(parseFiltersFromString('  foo   -  bar ')).toMatchObject({
      exclude: [],
      include: ['foo', 'bar'],
    });
  });
});

describe('serializeFilters', () => {
  it('round trips', () => {
    const text =
      'live -remix -demo islossless ext:flac,wav maxfs:1gb maxq:0 minbd:24 minfs:10mb minfif:8 minlen:60';
    expect(serializeFilters(parseFiltersFromString(text))).toBe(text);
  });

  it('writes nothing for empty filters', () => {
    expect(serializeFilters(parseFiltersFromString(''))).toBe('');
  });
});

describe('countFilters', () => {
  it('counts each filter in effect once', () => {
    expect(
      countFilters(
        parseFiltersFromString('a -b islossless ext:flac,mp3 maxq:0'),
      ),
    ).toBe(5);
  });
});

describe('filterResponse', () => {
  const response = {
    files: [
      { bitDepth: 24, filename: 'a\\album\\01.flac', sampleRate: 96_000 },
      { bitDepth: 16, filename: 'a\\album\\02.flac', sampleRate: 44_100 },
      { bitRate: 320, filename: 'a\\album\\03.mp3' },
      { filename: 'a\\other\\cover.jpg', size: 100 },
    ],
    lockedFiles: [],
    queueLength: 5,
    uploadSpeed: 1_000,
  };

  const filenames = (filters) =>
    filterResponse({
      filters: parseFiltersFromString(filters),
      response,
    }).files.map((file) => file.filename.split('\\').pop());

  it('keeps only the listed extensions', () => {
    expect(filenames('ext:flac')).toEqual(['01.flac', '02.flac']);
  });

  it('drops files without a bit depth or sample rate when a minimum is set', () => {
    expect(filenames('minbd:24')).toEqual(['01.flac']);
    expect(filenames('minsr:48')).toEqual(['01.flac']);
  });

  it('drops files over the maximum size', () => {
    expect(filenames('maxfs:50')).toEqual(['01.flac', '02.flac', '03.mp3']);
  });

  it('counts files per folder after the other filters', () => {
    expect(filenames('minfif:2')).toEqual(['01.flac', '02.flac', '03.mp3']);
    expect(filenames('ext:flac,jpg minfif:2')).toEqual(['01.flac', '02.flac']);
  });

  it('drops the whole response for long queues or slow users', () => {
    expect(filenames('maxq:4')).toEqual([]);
    expect(filenames('maxq:5')).toHaveLength(4);
    expect(filenames('minspeed:2k')).toEqual([]);
  });

  it('counts lossless files by extension when they report no bit depth', () => {
    const bare = {
      files: [
        { bitRate: 800, filename: 'x\\01.flac' },
        { bitRate: 320, filename: 'x\\02.mp3' },
      ],
      lockedFiles: [],
    };

    const names = (text) =>
      filterResponse({
        filters: parseFiltersFromString(text),
        response: bare,
      }).files.map((file) => file.filename);

    expect(names('islossless')).toEqual(['x\\01.flac']);
    expect(names('islossy')).toEqual(['x\\02.mp3']);
  });
});
