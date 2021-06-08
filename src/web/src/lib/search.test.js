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

    expect(search.filterResponse({ response, filters })).toMatchObject({
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

    expect(search.filterResponse({ response, filters })).toMatchObject({
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

    expect(search.filterResponse({ response, filters })).toMatchObject({
      files: []
    });
  });

  it('removes files with bitRate less than minBitRate', () => {
    const response = {
      files: [
        { bitRate: 100 },
        { bitRate: 99 }
      ]
    };

    const filters = { minBitRate: 100 };

    expect(search.filterResponse({ response, filters })).toMatchObject({
      files: [
        { bitRate: 100 }
      ]
    });
  });

  it('removes files with size less than minFileSize', () => {
    const response = {
      files: [
        { size: 100 },
        { size: 99 }
      ]
    };

    const filters = { minFileSize: 100 };

    expect(search.filterResponse({ response, filters })).toMatchObject({
      files: [
        { size: 100 }
      ]
    });
  });

  it('removes files with length less than minLength', () => {
    const response = {
      files: [
        { length: 100 },
        { length: 99 }
      ]
    };

    const filters = { minLength: 100 };

    expect(search.filterResponse({ response, filters })).toMatchObject({
      files: [
        { length: 100 }
      ]
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
        { filename: '/path/to/folder.jpg' }
      ]
    };

    it('removes files with filenames not containing included phrases', () => {
      const filters = { include: ['path', 'to', '.nfo'] };
  
      expect(search.filterResponse({ response, filters })).toMatchObject({
        files: [
          { filename: '/path/to/info.nfo' },
        ]
      });
    });
    
    it('removes files with filenames containing excluded phrases', () => {
      const filters = { exclude: ['bar', 'jpg', 'qux'] };
  
      expect(search.filterResponse({ response, filters })).toMatchObject({
        files: [
          { filename: '/path/to/foo.mp3' },
          { filename: '/path/to/baz.mp3' },
          { filename: '/path/to/info.nfo' },
        ]
      });
    });
  
    it('removes a mix of includes and excludes', () => {
      const filters = { 
        include: ['path', '.mp3'],
        exclude: ['foo', 'bar'] 
      };
  
      expect(search.filterResponse({ response, filters })).toMatchObject({
        files: [
          { filename: '/path/to/baz.mp3' },
          { filename: '/path/to/qux.mp3' },
        ]
      });
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

    expect(search.parseFiltersFromString('-foo -bar -baz qux')).toMatchObject({
      exclude: [ 'foo', 'bar', 'baz' ],
      include: [ 'qux' ]
    });

    expect(search.parseFiltersFromString('foo bar baz -qux')).toMatchObject({
      include: [ 'foo', 'bar', 'baz' ],
      exclude: [ 'qux' ]
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