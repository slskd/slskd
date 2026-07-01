import {
  buildBrowseUrl,
  decodeBrowseParams,
  fromBrowsePathSegments,
  toBrowsePathSegments,
} from './browseRoutes';

describe('browseRoutes', () => {
  it('builds a username-only browse URL', () => {
    expect(buildBrowseUrl({ username: 'some user' })).toBe(
      '/browse/some%20user',
    );
  });

  it('builds a canonical browse URL with directory path segments', () => {
    expect(
      buildBrowseUrl({
        directory: 'Music\\Artist Name\\Album/Disc 1',
        username: 'some/user',
      }),
    ).toBe('/browse/some%2Fuser/Music/Artist%20Name/Album/Disc%201');
  });

  it('preserves a configured urlBase', () => {
    expect(
      buildBrowseUrl({
        directory: 'Music\\Artist',
        urlBase: '/ui',
        username: 'alice',
      }),
    ).toBe('/ui/browse/alice/Music/Artist');
  });

  it('decodes browse params from react-router params', () => {
    expect(
      decodeBrowseParams({
        directory: 'Music/Artist%20Name/Album',
        username: 'some%2Fuser',
      }),
    ).toEqual({
      directory: 'Music\\Artist Name\\Album',
      username: 'some/user',
    });
  });

  it('round-trips directory separators through URL path segments', () => {
    const directory = 'A\\B/C';

    expect(fromBrowsePathSegments(toBrowsePathSegments(directory))).toBe(
      'A\\B\\C',
    );
  });
});
