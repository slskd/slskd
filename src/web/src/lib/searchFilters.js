// search result filters: a small text syntax (the filter box) that the filter form reads and writes

const sizeUnits = {
  '': 1,
  b: 1,
  g: 1_024 ** 3,
  gb: 1_024 ** 3,
  k: 1_024,
  kb: 1_024,
  m: 1_024 ** 2,
  mb: 1_024 ** 2,
};

const parseInteger = (value) => {
  if (!/^\d+$/u.test(String(value).trim())) return undefined;
  return Number.parseInt(value, 10);
};

// bytes; a bare number is bytes, and k/kb, m/mb and g/gb suffixes are accepted
export const parseSize = (value) => {
  const match = /^(\d+(?:\.\d+)?)([gkm]?b?)$/iu.exec(String(value).trim());
  if (!match) return undefined;
  return Math.round(Number(match[1]) * sizeUnits[match[2].toLowerCase()]);
};

export const formatSize = (bytes) => {
  for (const unit of ['gb', 'mb', 'kb']) {
    const value = bytes / sizeUnits[unit];
    if (value >= 1) return `${Number(value.toFixed(2))}${unit}`;
  }

  return String(bytes);
};

// seconds; m:ss and h:mm:ss are accepted too
export const parseDuration = (value) => {
  const parts = String(value).trim().split(':');
  if (parts.some((part) => !/^\d+$/u.test(part)) || parts.length > 3) {
    return undefined;
  }

  return parts.reduce((total, part) => total * 60 + Number(part), 0);
};

// Hz; values under 1000 are taken to be kHz, so 44.1 and 96 work
const parseSampleRate = (value) => {
  if (!/^\d+(?:\.\d+)?$/u.test(String(value).trim())) return undefined;
  const number = Number.parseFloat(value);
  return Math.round(number < 1_000 ? number * 1_000 : number);
};

// minimums are off at 0; maximums are off when undefined, since 0 is a meaningful maximum
export const valueFilters = {
  maxFileSize: {
    aliases: ['maxfs', 'maxfilesize'],
    format: formatSize,
    parse: parseSize,
  },
  maxLength: { aliases: ['maxlen', 'maxlength'], parse: parseDuration },
  maxQueueLength: {
    aliases: ['maxq', 'maxqueue', 'maxqueuelength'],
    parse: parseInteger,
  },
  minBitDepth: { aliases: ['minbd', 'minbitdepth'], parse: parseInteger },
  minBitRate: { aliases: ['minbr', 'minbitrate'], parse: parseInteger },
  minFileSize: {
    aliases: ['minfs', 'minfilesize'],
    format: formatSize,
    parse: parseSize,
  },
  minFilesInFolder: {
    aliases: ['minfif', 'minfilesinfolder'],
    parse: parseInteger,
  },
  minLength: { aliases: ['minlen', 'minlength'], parse: parseDuration },
  minSampleRate: {
    aliases: ['minsr', 'minsamplerate'],
    parse: parseSampleRate,
  },
  minUploadSpeed: {
    aliases: ['minspeed', 'minuploadspeed'],
    format: formatSize,
    parse: parseSize,
  },
};

const flagFilters = {
  iscbr: 'isCBR',
  islossless: 'isLossless',
  islossy: 'isLossy',
  isvbr: 'isVBR',
};

const isMaximum = (key) => key.startsWith('max');

const isSet = (key, value) =>
  isMaximum(key) ? value !== undefined && value !== null : Boolean(value);

export const emptyFilters = () => ({
  exclude: [],
  extensions: [],
  include: [],
  isCBR: false,
  isLossless: false,
  isLossy: false,
  isVBR: false,
  ...Object.fromEntries(
    Object.keys(valueFilters).map((key) => [
      key,
      isMaximum(key) ? undefined : 0,
    ]),
  ),
});

const valueFilterByAlias = Object.fromEntries(
  Object.entries(valueFilters).flatMap(([key, { aliases }]) =>
    aliases.map((alias) => [alias, key]),
  ),
);

const splitExtensions = (value) =>
  value
    .split(',')
    .map((extension) => extension.trim().replace(/^\./u, ''))
    .filter(Boolean);

export const parseFiltersFromString = (string = '') => {
  const filters = emptyFilters();

  for (const token of string.toLowerCase().split(/\s+/u).filter(Boolean)) {
    const separator = token.indexOf(':');

    if (separator > 0) {
      const name = token.slice(0, separator);
      const value = token.slice(separator + 1);

      if (name === 'ext') {
        filters.extensions.push(...splitExtensions(value));
      } else if (valueFilterByAlias[name]) {
        const key = valueFilterByAlias[name];
        const parsed = valueFilters[key].parse(value);
        if (parsed !== undefined) filters[key] = parsed;
      }

      // unknown name:value tokens are ignored rather than treated as words
    } else if (flagFilters[token]) {
      filters[flagFilters[token]] = true;
    } else if (token.startsWith('-')) {
      if (token.length > 1) filters.exclude.push(token.slice(1));
    } else {
      filters.include.push(token);
    }
  }

  filters.extensions = [...new Set(filters.extensions)];
  return filters;
};

// the inverse of parseFiltersFromString, used when filters are edited through the form
export const serializeFilters = (filters = {}) => {
  const tokens = [
    ...(filters.include ?? []),
    ...(filters.exclude ?? []).map((term) => `-${term}`),
  ];

  for (const [flag, key] of Object.entries(flagFilters)) {
    if (filters[key]) tokens.push(flag);
  }

  if (filters.extensions?.length) {
    tokens.push(`ext:${filters.extensions.join(',')}`);
  }

  for (const [key, { aliases, format = String }] of Object.entries(
    valueFilters,
  )) {
    if (isSet(key, filters[key])) {
      tokens.push(`${aliases[0]}:${format(filters[key])}`);
    }
  }

  return tokens.join(' ');
};

// the number of filters in effect, for display
export const countFilters = (filters = {}) =>
  (filters.include?.length ?? 0) +
  (filters.exclude?.length ?? 0) +
  (filters.extensions?.length ? 1 : 0) +
  Object.values(flagFilters).filter((key) => filters[key]).length +
  Object.keys(valueFilters).filter((key) => isSet(key, filters[key])).length;

export const getExtension = (filename = '') => {
  const name = filename.split(/[/\\]/u).pop();
  const dot = name.lastIndexOf('.');
  return dot > 0 ? name.slice(dot + 1).toLowerCase() : '';
};

const losslessExtensions = new Set([
  'aif',
  'aiff',
  'alac',
  'ape',
  'dff',
  'dsf',
  'flac',
  'tta',
  'wav',
  'wv',
]);

// lossless files report a bit depth and sample rate; some clients leave those out, so the
// extension counts too
export const isLosslessFile = (file) =>
  Boolean(file.bitDepth && file.sampleRate) ||
  losslessExtensions.has(getExtension(file.filename));

const directoryOf = (filename = '') => {
  const separator = Math.max(
    filename.lastIndexOf('\\'),
    filename.lastIndexOf('/'),
  );
  return separator > 0 ? filename.slice(0, separator) : '';
};

// each check takes the filters and a file, and returns whether the file passes
const passesEncoding = (filters, file) => {
  const { isCBR, isLossless, isLossy, isVBR } = filters;
  const { isVariableBitRate } = file;

  if (isCBR && (isVariableBitRate === undefined || isVariableBitRate))
    return false;
  if (isVBR && (isVariableBitRate === undefined || !isVariableBitRate))
    return false;
  if (isLossless && !isLosslessFile(file)) return false;
  if (isLossy && isLosslessFile(file)) return false;

  return true;
};

const passesQuality = (filters, file) => {
  const { extensions = [], minBitDepth, minBitRate, minSampleRate } = filters;
  const { bitDepth, bitRate, filename = '', sampleRate } = file;

  if (extensions.length > 0 && !extensions.includes(getExtension(filename)))
    return false;
  if (bitRate < minBitRate) return false;
  // lossy files carry no bit depth or sample rate, so they can't meet a minimum
  if (minBitDepth && !(bitDepth >= minBitDepth)) return false;
  if (minSampleRate && !(sampleRate >= minSampleRate)) return false;

  return true;
};

const passesSize = (filters, file) => {
  const { maxFileSize, maxLength, minFileSize, minLength } = filters;
  const { length, size } = file;

  if (size < minFileSize) return false;
  if (isSet('maxFileSize', maxFileSize) && size > maxFileSize) return false;
  if (length < minLength) return false;
  if (isSet('maxLength', maxLength) && length > maxLength) return false;

  return true;
};

const passesWords = (filters, file) => {
  const { exclude = [], include = [] } = filters;
  const name = (file.filename ?? '').toLowerCase();

  return (
    include.every((term) => name.includes(term)) &&
    !exclude.some((term) => name.includes(term))
  );
};

const fileFilter = (filters) => (file) =>
  passesEncoding(filters, file) &&
  passesQuality(filters, file) &&
  passesSize(filters, file) &&
  passesWords(filters, file);

export const filterResponse = ({
  filters = emptyFilters(),
  response = {
    files: [],
    lockedFiles: [],
  },
}) => {
  const { files = [], lockedFiles = [] } = response;
  const { maxQueueLength, minFilesInFolder, minUploadSpeed } = filters;

  if (
    (isSet('maxQueueLength', maxQueueLength) &&
      response.queueLength > maxQueueLength) ||
    (minUploadSpeed && !(response.uploadSpeed >= minUploadSpeed))
  ) {
    return {
      ...response,
      fileCount: 0,
      files: [],
      lockedFileCount: 0,
      lockedFiles: [],
    };
  }

  const keep = fileFilter(filters);
  let filteredFiles = files.filter(keep);
  let filteredLockedFiles = lockedFiles.filter(keep);

  // counted per folder after the other filters, so "at least 8 FLACs in the folder" works
  if (minFilesInFolder) {
    const counts = {};
    for (const file of [...filteredFiles, ...filteredLockedFiles]) {
      const directory = directoryOf(file.filename);
      counts[directory] = (counts[directory] ?? 0) + 1;
    }

    const bigEnough = (file) =>
      counts[directoryOf(file.filename)] >= minFilesInFolder;
    filteredFiles = filteredFiles.filter(bigEnough);
    filteredLockedFiles = filteredLockedFiles.filter(bigEnough);
  }

  return {
    ...response,
    fileCount: filteredFiles.length,
    files: filteredFiles,
    lockedFileCount: filteredLockedFiles.length,
    lockedFiles: filteredLockedFiles,
  };
};

const presetsKey = 'slskd-search-filter-presets';
const defaultFilterKey = 'slskd-search-default-filter';

export const builtInFilterPresets = [
  { builtIn: true, filters: 'islossless', name: 'Lossless' },
  { builtIn: true, filters: 'islossless minbd:24', name: 'Hi-res lossless' },
  { builtIn: true, filters: 'ext:mp3 minbr:320', name: 'MP3 320' },
  { builtIn: true, filters: 'minfif:5', name: 'Whole folders (5+ files)' },
  { builtIn: true, filters: 'maxq:10', name: 'Short queues' },
];

export const getFilterPresets = () => {
  try {
    const saved = JSON.parse(localStorage.getItem(presetsKey));
    return Array.isArray(saved)
      ? saved.filter(
          (preset) =>
            typeof preset?.name === 'string' &&
            typeof preset?.filters === 'string',
        )
      : [];
  } catch {
    return [];
  }
};

export const saveFilterPreset = ({ filters, name }) => {
  const updated = [
    ...getFilterPresets().filter((preset) => preset.name !== name),
    { filters, name },
  ].sort((a, b) => a.name.localeCompare(b.name));
  localStorage.setItem(presetsKey, JSON.stringify(updated));
  return updated;
};

export const deleteFilterPreset = (name) => {
  const updated = getFilterPresets().filter((preset) => preset.name !== name);
  localStorage.setItem(presetsKey, JSON.stringify(updated));
  return updated;
};

export const getDefaultFilter = () =>
  localStorage.getItem(defaultFilterKey) ?? '';

export const setDefaultFilter = (filters) => {
  if (filters) {
    localStorage.setItem(defaultFilterKey, filters);
  } else {
    localStorage.removeItem(defaultFilterKey);
  }
};
