import api from './api';

export const getAll = async () => {
  return (await api.get('/searches')).data;
};

export const stop = ({ id }) => {
  return api.put(`/searches/${encodeURIComponent(id)}`);
};

export const remove = ({ id }) => {
  return api.delete(`/searches/${encodeURIComponent(id)}`);
};

export const create = ({ id, searchText }) => {
  return api.post('/searches', { id, searchText });
};

export const getStatus = async ({ id, includeResponses = false }) => {
  return (await api.get(`/searches/${encodeURIComponent(id)}?includeResponses=${includeResponses}`)).data;
};

export const getResponses = async ({ id }) => {
  const response = (await api.get(`/searches/${encodeURIComponent(id)}/responses`)).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from searches API', response);
    return undefined;
  }

  return response;
};

export const parseFiltersFromString = (string) => {
  const filters = {
    minBitRate: 0,
    minBitDepth: 0,
    minFileSize: 0,
    minLength: 0,
    minFilesInFolder: 0,
    include: [],
    exclude: [],
    isVBR: false,
    isCBR: false,
    isLossless: false,
    isLossy: false,
  };

  const getNthMatch = (string, regex, n) => {
    const match = string.match(regex);
  
    if (match) {
      return parseInt(match[n], 10);
    }
  };

  filters.minBitRate = getNthMatch(string, /(minbr|minbitrate):([0-9]+)/i, 2) || filters.minBitRate;
  filters.minBitDepth = getNthMatch(string, /(minbd|minbitdepth):([0-9]+)/i, 2) || filters.minBitDepth;
  filters.minFileSize = getNthMatch(string, /(minfs|minfilesize):([0-9]+)/i, 2) || filters.minFileSize;
  filters.minLength = getNthMatch(string, /(minlen|minlength):([0-9]+)/i, 2) || filters.minLength;
  filters.minFilesInFolder = getNthMatch(string, /(minfif|minfilesinfolder):([0-9]+)/i, 2) || filters.minFilesInFolder;
  
  filters.isVBR = !!string.match(/isvbr/i);
  filters.isCBR = !!string.match(/iscbr/i);
  filters.isLossless = !!string.match(/islossless/i);
  filters.isLossy = !!string.match(/islossy/i);

  let terms = string.toLowerCase().split(' ')
    .filter(term =>
      !term.includes(':') && term !== 'isvbr' && term !== 'iscbr' && term !== 'islossless' && term !== 'islossy');

  filters.include = terms.filter(term => !term.startsWith('-'));
  filters.exclude = terms.filter(term => term.startsWith('-')).map(term => term.slice(1));

  return filters;
};

export const filterResponse = ({ 
  filters = {
    minBitRate: 0,
    minBitDepth: 0,
    minFileSize: 0,
    minLength: 0,
    include: [],
    exclude: [],
    isVBR: false,
    isCBR: false,
    isLossless: false,
    isLossy: false,
  },
  response = { 
    files: [],
    lockedFiles: [],
  }, 
}) => {
  let { files = [], lockedFiles = [] } = response;

  if (response.fileCount + response.lockedFileCount < filters.minFilesInFolder) {
    return { ...response, files: [] };
  }

  const filterFiles = (files) => files.filter(file => {
    const { bitRate, size, length, filename, sampleRate, bitDepth, isVariableBitRate } = file;
    const {
      isCBR, isVBR, isLossless, isLossy,
      minBitRate, minBitDepth, minFileSize, minLength,
      include = [], exclude = [],
    } = filters;

    if (isCBR && (isVariableBitRate === undefined || isVariableBitRate)) return false;    
    if (isVBR && (isVariableBitRate === undefined || !isVariableBitRate)) return false;
    if (isLossless && (!sampleRate || !bitDepth)) return false;
    if (isLossy && (sampleRate || bitDepth)) return false;
    if (bitRate < minBitRate) return false;
    if (bitDepth < minBitDepth) return false;
    if (size < minFileSize) return false;
    if (length < minLength) return false;

    if (include.length > 0 && include.filter(term => filename.toLowerCase().includes(term)).length !== include.length) {
      return false;
    }

    if (exclude.length > 0 && exclude.filter(term => filename.toLowerCase().includes(term)).length !== 0) return false;

    return true;
  });

  const filteredFiles = filterFiles(files);
  const filteredLockedFiles = filterFiles(lockedFiles);

  return { 
    ...response,
    fileCount: filteredFiles.length,
    lockedFileCount: filteredLockedFiles.length,
    files: filteredFiles, 
    lockedFiles: filteredLockedFiles,
  };
};