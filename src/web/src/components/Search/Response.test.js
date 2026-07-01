import { buildBrowseUrl } from '../Browse/browseRoutes';

describe('search response browse links', () => {
  it('builds a Browse link for a search result directory', () => {
    expect(
      buildBrowseUrl({
        directory: 'Music\\Artist Name\\Album',
        username: 'some user',
      }),
    ).toBe('/browse/some%20user/Music/Artist%20Name/Album');
  });
});
