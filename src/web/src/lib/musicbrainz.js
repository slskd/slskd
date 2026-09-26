import api from './api';

export const searchReleases = async ({
  dismax = false,
  limit = 25,
  offset = 0,
  query,
}) =>
  (
    await api.get('/musicbrainz/releases', {
      params: { dismax, limit, offset, query },
    })
  ).data;

export const getRelease = async ({ id }) =>
  (await api.get(`/musicbrainz/releases/${encodeURIComponent(id)}`)).data;

// covers come through slskd, which tries the Cover Art Archive and then Deezer and iTunes;
// they're fetched with the session token, since an <img> can't send it, and kept as object URLs
const covers = new Map();

const fetchCover = async ({ key, release, size }) => {
  try {
    const response = await api.get(
      `/musicbrainz/releases/${encodeURIComponent(release.id)}/cover`,
      {
        params: {
          artist: release.artist,
          group: release.releaseGroupId,
          size,
          title: release.title,
        },
        responseType: 'blob',
      },
    );

    return URL.createObjectURL(response.data);
  } catch (error) {
    // a missing cover stays missing; anything else may work next time
    if (error?.response?.status !== 404) covers.delete(key);
    return undefined;
  }
};

export const getCoverUrl = ({ release, size = 250 }) => {
  const key = `${release.id}:${size}`;

  if (!covers.has(key)) {
    covers.set(key, fetchCover({ key, release, size }));
  }

  return covers.get(key);
};

export const listSaved = async () => (await api.get('/musicbrainz/saved')).data;

export const getSaved = async ({ id }) => {
  try {
    return (await api.get(`/musicbrainz/saved/${encodeURIComponent(id)}`)).data;
  } catch (error) {
    if (error?.response?.status === 404) return undefined;
    throw error;
  }
};

export const saveRelease = async ({ query, release, searchId }) =>
  (
    await api.put(`/musicbrainz/saved/${encodeURIComponent(release.id)}`, {
      query,
      release,
      searchId,
    })
  ).data;

export const removeSaved = ({ id }) =>
  api.delete(`/musicbrainz/saved/${encodeURIComponent(id)}`);

export const removeAllSaved = async () =>
  (await api.delete('/musicbrainz/saved')).data;

export const releaseUrl = (id) => `https://musicbrainz.org/release/${id}`;

const mbid = '[\\da-f]{8}-[\\da-f]{4}-[\\da-f]{4}-[\\da-f]{4}-[\\da-f]{12}';

const quote = (text) =>
  `"${text.replaceAll(/["\\]/gu, (character) => `\\${character}`)}"`;

// works out what was typed into the release search box: a MusicBrainz link, a bare MBID,
// "artist - title", or plain text
export const parseReleaseInput = (input = '') => {
  const text = input.trim();

  const link = new RegExp(
    `musicbrainz\\.org/(release|release-group|artist)/(${mbid})`,
    'iu',
  ).exec(text);

  if (link) {
    const [, type, id] = link;
    return { id: id.toLowerCase(), type: type.toLowerCase() };
  }

  if (new RegExp(`^${mbid}$`, 'iu').test(text)) {
    return { id: text.toLowerCase(), type: 'release' };
  }

  const dash = /\s[‐–—-]\s/u.exec(text);
  const artist = dash && text.slice(0, dash.index).trim();
  const title = dash && text.slice(dash.index + dash[0].length).trim();

  if (artist && title) {
    return {
      dismax: false,
      query: `artist:${quote(artist)} AND release:${quote(title)}`,
      type: 'query',
    };
  }

  return { dismax: true, query: text, type: 'query' };
};

// the search to run for a parsed input that isn't a single release
export const searchFor = (parsed) => {
  if (parsed.type === 'release-group') {
    return { dismax: false, query: `rgid:${parsed.id}` };
  }

  if (parsed.type === 'artist') {
    return { dismax: false, query: `arid:${parsed.id}` };
  }

  return { dismax: parsed.dismax, query: parsed.query };
};

export const formatDuration = (milliseconds) => {
  if (milliseconds === undefined || milliseconds === null) return '';
  const seconds = Math.round(milliseconds / 1_000);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
};

export const releaseYear = (release) => release?.date?.slice(0, 4);
