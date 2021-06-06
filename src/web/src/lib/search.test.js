import * as search from './search';

describe('isConstantBitRate', () => {
  it('returns true given a valid MPEG-3 bitrate', () => {
    const bitrates = [8,16,24,32,40,48,56,64,80,96,112,128,144,160,192,224,256,320];

    bitrates.forEach(br => {
      expect(search.isConstantBitRate(br)).toBe(true);
    });
  });

  it('returns false given an invalid MPEG-3 bitrate', () => {
    const bitrates = [5,7,65,150,321];

    bitrates.forEach(br => {
      expect(search.isConstantBitRate(br)).toBe(false);
    });
  });

  it('returns false given null or undefined', () => {
    expect(search.isConstantBitRate(null)).toBe(false);
    expect(search.isConstantBitRate()).toBe(false);
  });
});

describe('filterResponse', () => {
  it('removes VBR files if "iscbr" is specified', () => {
    const response = {
      files: [
        { bitRate: 123 },
        { bitRate: 320 }
      ]
    };

    const filters = { isCBR: true };

    expect(search.filterResponse({ response, filters })).toStrictEqual({
      files: [
        { bitRate: 320 }
      ]
    });
  });

  it('removes CBR files if "isvbr" is specified', () => {
    const response = {
      files: [
        { bitRate: 123 },
        { bitRate: 320 }
      ]
    };

    const filters = { isVBR: true };

    expect(search.filterResponse({ response, filters })).toStrictEqual({
      files: [
        { bitRate: 123 }
      ]
    });
  });

  it('removes all files if "iscbr" and "isvbr" are both specified', () => {
    const response = {
      files: [
        { bitRate: 123 },
        { bitRate: 320 }
      ]
    };

    const filters = { isCBR: true, isVBR: true };

    expect(search.filterResponse({ response, filters })).toStrictEqual({
      files: []
    });
  });
});

describe('parseFiltersFromString', () => {
  it('returns correct minBitrate', () => {
    expect(search.parseFiltersFromString('foo minbr:42 bar')).toMatchObject({
      minBitRate: 42
    });

    expect(search.parseFiltersFromString('foo minbitrate:123 bar')).toMatchObject({
      minBitRate: 123
    });
  });

  it('returns correct minFileSize', () => {
    expect(search.parseFiltersFromString('foo minfs:42 bar')).toMatchObject({
      minFileSize: 42
    });

    expect(search.parseFiltersFromString('foo minfilesize:123 bar')).toMatchObject({
      minFileSize: 123
    });
  });

  it('returns correct minLength', () => {
    expect(search.parseFiltersFromString('foo minlen:42 bar')).toMatchObject({
      minLength: 42
    });

    expect(search.parseFiltersFromString('foo minlength:123 bar')).toMatchObject({
      minLength: 123
    });
  });

  it('returns correct minFilesInFolder', () => {
    expect(search.parseFiltersFromString('foo minfif:42 bar')).toMatchObject({
      minFilesInFolder: 42
    });

    expect(search.parseFiltersFromString('foo minfilesinfolder:123 bar')).toMatchObject({
      minFilesInFolder: 123
    });
  });

  it('returns correct list of terms', () => {
    expect(search.parseFiltersFromString('foo minbr:42 bar')).toMatchObject({
      include: [ 'foo', 'bar']
    });

    expect(search.parseFiltersFromString('foo iscbr isvbr bar')).toMatchObject({
      include: [ 'foo', 'bar']
    });

    expect(search.parseFiltersFromString('foo some:thing bar')).toMatchObject({
      include: [ 'foo', 'bar']
    });

    expect(search.parseFiltersFromString('foo -bar')).toMatchObject({
      include: [ 'foo' ],
      exclude: [ 'bar' ]
    });
  });

  it('returns isVBR and isCBR if terms are present', () => {
    expect(search.parseFiltersFromString('isvbr')).toMatchObject({
      isVBR: true
    });

    expect(search.parseFiltersFromString('iscbr')).toMatchObject({
      isCBR: true
    });
  });

  it('returns expected filters given a bit of everything', () => {
    expect(search.parseFiltersFromString('big -mix of:everything isvbr iscbr minbr:42')).toMatchObject({
      include: [ 'big' ],
      exclude: [ 'mix' ],
      isVBR: true,
      isCBR: true,
      minBitRate: 42
    });
  });
});