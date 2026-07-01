const trimSlashes = (value = '') => value.replace(/^\/+|\/+$/g, '');

export const toBrowsePathSegments = (directory = '') =>
  trimSlashes(directory)
    .split(/[\\/]+/)
    .filter(Boolean)
    .map((segment) => encodeURIComponent(segment))
    .join('/');

export const fromBrowsePathSegments = (path = '') =>
  trimSlashes(path)
    .split('/')
    .filter(Boolean)
    .map((segment) => decodeURIComponent(segment))
    .join('\\');

export const buildBrowseUrl = ({ directory = '', urlBase = '', username }) => {
  if (!username) {
    return `${urlBase}/browse`;
  }

  const encodedUsername = encodeURIComponent(username);
  const encodedDirectory = toBrowsePathSegments(directory);

  return `${urlBase}/browse/${encodedUsername}${
    encodedDirectory ? `/${encodedDirectory}` : ''
  }`;
};

export const decodeBrowseParams = (params = {}) => ({
  directory: fromBrowsePathSegments(params.directory),
  username: params.username ? decodeURIComponent(params.username) : '',
});
