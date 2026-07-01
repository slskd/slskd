/* eslint-disable unicorn/prevent-abbreviations */
const trimSlashes = (value = '') => value.replaceAll(/^\/+|\/+$/gu, '');

export const toBrowsePathSegments = (directory = '') =>
  trimSlashes(directory)
    .split(/[/\\]+/gu)
    .filter(Boolean)
    .map((segment) => encodeURIComponent(segment))
    .join('/');

export const fromBrowsePathSegments = (path = '', separator = '\\') =>
  trimSlashes(path)
    .split('/')
    .filter(Boolean)
    .map((segment) => decodeURIComponent(segment))
    .join(separator);

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

export const decodeBrowseParams = (parameters = {}, separator = '\\') => ({
  directory: fromBrowsePathSegments(parameters.directory, separator),
  username: parameters.username ? decodeURIComponent(parameters.username) : '',
});
