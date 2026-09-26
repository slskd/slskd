import { formatDuration, parseReleaseInput, searchFor } from './musicbrainz';

const id = 'af187112-85cd-49fa-b6a2-d0b4c20a7e56';

describe('parseReleaseInput', () => {
  it('reads MusicBrainz links', () => {
    expect(
      parseReleaseInput(`https://musicbrainz.org/release/${id}/discids`),
    ).toEqual({ id, type: 'release' });
    expect(
      parseReleaseInput(`musicbrainz.org/release-group/${id.toUpperCase()}`),
    ).toEqual({ id, type: 'release-group' });
    expect(
      parseReleaseInput(`https://beta.musicbrainz.org/artist/${id}`),
    ).toEqual({
      id,
      type: 'artist',
    });
  });

  it('reads a bare MBID as a release', () => {
    expect(parseReleaseInput(`  ${id} `)).toEqual({ id, type: 'release' });
  });

  it('splits "artist - title" into a fielded query', () => {
    expect(parseReleaseInput('Aphex Twin – Selected "Ambient" Works')).toEqual({
      dismax: false,
      query: 'artist:"Aphex Twin" AND release:"Selected \\"Ambient\\" Works"',
      type: 'query',
    });
  });

  it('searches anything else as plain text', () => {
    expect(parseReleaseInput('selected ambient works 85-92')).toEqual({
      dismax: true,
      query: 'selected ambient works 85-92',
      type: 'query',
    });
  });
});

describe('searchFor', () => {
  it('lists the releases in a release group or by an artist', () => {
    expect(searchFor({ id, type: 'release-group' })).toEqual({
      dismax: false,
      query: `rgid:${id}`,
    });
    expect(searchFor({ id, type: 'artist' }).query).toBe(`arid:${id}`);
  });
});

describe('formatDuration', () => {
  it('formats milliseconds as m:ss', () => {
    expect(formatDuration(294_000)).toBe('4:54');
    expect(formatDuration(59_600)).toBe('1:00');
    expect(formatDuration(undefined)).toBe('');
  });
});
