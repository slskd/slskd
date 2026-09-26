// matches Soulseek search results against a MusicBrainz release: files are grouped into
// album folders, each folder's files are paired with the release's tracks, and the
// folders are ranked by how much of the release they hold and how good the files are

import { getExtension, isLosslessFile } from './searchFilters';

export const audioExtensions = new Set([
  'aac',
  'aif',
  'aiff',
  'alac',
  'ape',
  'dff',
  'dsf',
  'flac',
  'm4a',
  'mp3',
  'mpc',
  'oga',
  'ogg',
  'opus',
  'tta',
  'wav',
  'wma',
  'wv',
]);

export const isAudio = (filename) =>
  audioExtensions.has(getExtension(filename));

// lowercase words without accents or punctuation, for comparing names
export const normalize = (text = '') =>
  text
    .normalize('NFKD')
    .replaceAll(/\p{M}/gu, '')
    .toLowerCase()
    .replaceAll('&', ' and ')
    .replaceAll(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();

const words = (text) => normalize(text).split(' ').filter(Boolean);

const separatorOf = (filename) => (filename.includes('\\') ? '\\' : '/');

const splitPath = (filename) => filename.split(/[/\\]/u);

const discFolder =
  /^(?:cd|dis[ck]|disque|part)\s*(?:[#.]\s*)?(\d{1,2})(?!\d)/iu;

// the folder an album lives in; disc folders such as "CD1" or "Disc 2 - Bonus" are merged
// into their parent, and the disc number is kept as a hint
export const albumFolderOf = (filename) => {
  const separator = separatorOf(filename);
  const parts = splitPath(filename).slice(0, -1);
  const disc = discFolder.exec(parts.at(-1) ?? '');

  if (disc && parts.length > 1) {
    return {
      disc: Number(disc[1]),
      folder: parts.slice(0, -1).join(separator),
      subfolder: parts.at(-1),
    };
  }

  return {
    disc: undefined,
    folder: parts.join(separator),
    subfolder: undefined,
  };
};

const stripExtension = (name) => name.replace(/\.[^.]{1,5}$/u, '');

// reads the track number (and disc number or vinyl side, if any) from a file name, and
// returns the words that remain
export const parseTrackFilename = (filename) => {
  const name = stripExtension(splitPath(filename).at(-1));

  const patterns = [
    // "1-01 Title", "2.05 Title", "CD1-01 Title"
    {
      pattern: /^\s*(?:(?:cd|dis[ck])\s*)?(\d{1,2})[.-](\d{2})(?!\d)/iu,
      read: (match) => ({ disc: Number(match[1]), track: Number(match[2]) }),
    },
    // "A1 Title", "B2. Title"
    {
      pattern: /^\s*([a-h])(\d{1,2})(?=[\s).\]_-]|$)/iu,
      read: (match) => ({
        code: `${match[1]}${Number(match[2])}`.toLowerCase(),
      }),
    },
    // "01 Title", "01. Title", "(01) Title", "101 Title", "Track 01"
    {
      pattern: /^\s*(?:track\s*)?[([]?(\d{1,3})(?=[\s).\]_-]|$)/iu,
      read: (match) => ({ track: Number(match[1]) }),
    },
    // "Artist - 01 - Title"
    {
      pattern: /\s[-–]\s*(\d{1,2})\s*[-–.]\s/u,
      read: (match) => ({ track: Number(match[1]) }),
    },
  ];

  for (const { pattern, read } of patterns) {
    const match = pattern.exec(name);
    if (match) {
      const rest = `${name.slice(0, match.index)} ${name.slice(match.index + match[0].length)}`;
      return { ...read(match), words: words(rest) };
    }
  }

  return { words: words(name) };
};

const editDistanceAtMostOne = (a, b) => {
  if (Math.abs(a.length - b.length) > 1) return false;

  let index = 0;
  while (index < a.length && index < b.length && a[index] === b[index]) {
    index += 1;
  }

  const restA = a.slice(index + 1);
  const restB = b.slice(index + 1);

  return (
    restA === restB ||
    a.slice(index) === b.slice(index + 1) ||
    a.slice(index + 1) === b.slice(index)
  );
};

// long words may differ by a typo
const sameWord = (a, b) =>
  a === b || (Math.min(a.length, b.length) >= 5 && editDistanceAtMostOne(a, b));

// how well the words left in a file name match a track title, from 0 to 1. words from the
// artist or album name are ignored, since file names often include them
const titleSimilarity = (titleWords, fileWords, ignored) => {
  if (titleWords.length === 0 || fileWords.length === 0) return 0;

  const found = titleWords.filter((word) =>
    fileWords.some((fileWord) => sameWord(word, fileWord)),
  ).length;

  if (found === 0) return 0;

  const unexplained = fileWords.filter(
    (fileWord) =>
      !ignored.has(fileWord) &&
      !/^\d+$/u.test(fileWord) &&
      !titleWords.some((word) => sameWord(word, fileWord)),
  ).length;

  const recall = found / titleWords.length;
  const precision = found / (found + unexplained);

  return recall * 0.75 + precision * 0.25;
};

// 1 when the number in the file name is this track's, less when it is ambiguous, and
// negative when it is some other track's
const numberScore = (track, parsed, multiDisc) => {
  if (parsed.code) {
    return parsed.code === track.number?.toLowerCase() ? 1 : -0.5;
  }

  if (parsed.track === undefined) return 0;

  let disc = parsed.disc ?? parsed.discHint;
  let number = parsed.track;

  // "101" on a multi-disc release is disc 1, track 1
  if (number >= 100 && disc === undefined && multiDisc) {
    disc = Math.floor(number / 100);
    number %= 100;
  }

  if (number !== track.position) {
    // some rips of multi-disc releases number the tracks straight through
    return multiDisc && disc === undefined && number === track.overall
      ? 0.8
      : -0.5;
  }

  if (!multiDisc) return 1;
  if (disc === undefined) return 0.5;
  return disc === track.disc ? 1 : -0.5;
};

// compares lengths; lengths within a few seconds are strong evidence, and far-off lengths
// are strong evidence against
const durationScore = (track, file) => {
  if (!track.length || !file.length) return 0;

  const delta = Math.abs(file.length - track.length / 1_000);
  if (delta <= 3) return 1;
  if (delta <= 8) return 0.4;
  if (delta <= 20) return -0.3;
  return -1;
};

export const scorePair = ({ file, ignored, multiDisc, parsed, track }) => {
  const title = titleSimilarity(track.words, parsed.words, ignored);
  const number = numberScore(track, parsed, multiDisc);
  const duration = durationScore(track, file);
  const total = title * 2 + number + duration;

  // a length more than 20 seconds out means a different version, whatever the name says.
  // an exact title is enough otherwise, since some folders number tracks per vinyl side or
  // come from a master with different gaps
  const accepted =
    duration > -1 &&
    (title >= 0.95 ||
      (total >= 1.5 && (title >= 0.5 || (number >= 0.8 && duration >= 1))));

  return { accepted, duration, number, title, total };
};

export const releaseTracks = (release) => {
  let overall = 0;

  return (release?.media ?? []).flatMap((medium) =>
    (medium.tracks ?? []).map((track) => {
      overall += 1;
      return {
        ...track,
        disc: medium.position,
        overall,
        words: words(track.title),
      };
    }),
  );
};

// pairs files with tracks, best pairs first, each file and track used once
export const matchFiles = ({ files, release }) => {
  const tracks = releaseTracks(release);
  const multiDisc = (release?.media?.length ?? 0) > 1;
  const ignored = new Set([
    ...words(release?.artist),
    ...words(release?.title),
  ]);

  const parsedFiles = files.map((file) => ({
    ...parseTrackFilename(file.filename),
    discHint: albumFolderOf(file.filename).disc,
  }));

  const pairs = [];
  for (const [trackIndex, track] of tracks.entries()) {
    for (const [fileIndex, file] of files.entries()) {
      const score = scorePair({
        file,
        ignored,
        multiDisc,
        parsed: parsedFiles[fileIndex],
        track,
      });
      if (score.accepted) pairs.push({ fileIndex, score, trackIndex });
    }
  }

  pairs.sort((a, b) => b.score.total - a.score.total);

  const fileFor = new Map();
  const usedFiles = new Set();
  for (const { fileIndex, score, trackIndex } of pairs) {
    if (!fileFor.has(trackIndex) && !usedFiles.has(fileIndex)) {
      fileFor.set(trackIndex, { file: files[fileIndex], score });
      usedFiles.add(fileIndex);
    }
  }

  return {
    extras: files.filter((_file, index) => !usedFiles.has(index)),
    matches: tracks.map((track, index) => ({
      track,
      ...fileFor.get(index),
    })),
  };
};

const mostCommon = (values) => {
  const counts = new Map();
  for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1);
  return [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0];
};

// a label such as "FLAC 24/96" or "MP3 320", and a rank for sorting by quality
export const summarizeQuality = (files) => {
  if (files.length === 0) return { label: '', rank: 0 };

  const extensions = [
    ...new Set(files.map((file) => getExtension(file.filename))),
  ];
  const format = extensions
    .map((extension) => extension.toUpperCase())
    .join(' + ');
  if (files.every(isLosslessFile)) {
    const described = files.filter((file) => file.bitDepth && file.sampleRate);

    // some clients don't report bit depth and sample rate for lossless files
    if (described.length < files.length) {
      return { label: format, rank: 10_000 + 1_600 };
    }

    const bitDepth = Math.min(...files.map((file) => file.bitDepth));
    const sampleRate = Math.min(...files.map((file) => file.sampleRate));
    return {
      label: `${format} ${bitDepth}/${Number((sampleRate / 1_000).toFixed(1))}`,
      rank: 10_000 + bitDepth * 100 + sampleRate / 1_000,
    };
  }

  const bitRates = files.map((file) => file.bitRate).filter(Boolean);
  if (bitRates.length === 0) return { label: format, rank: 1 };

  const bitRate = Math.min(...bitRates);
  const variable = mostCommon(
    files.map((file) => Boolean(file.isVariableBitRate)),
  );
  return {
    label: variable
      ? `${format} ~${Math.round(bitRates.reduce((sum, rate) => sum + rate, 0) / bitRates.length)} VBR`
      : `${format} ${bitRate}`,
    rank: bitRate,
  };
};

export const evaluateCandidate = (group, release) => {
  const { extras, matches } = matchFiles({ files: group.audio, release });
  const matched = matches.filter((match) => match.file);
  const matchedFiles = matched.map((match) => match.file);

  return {
    ...group,
    complete: matches.length > 0 && matched.length === matches.length,
    extras,
    locked: matchedFiles.some((file) => file.locked),
    matchedCount: matched.length,
    matches,
    quality: summarizeQuality(matchedFiles),
    size: matchedFiles.reduce((total, file) => total + (file.size ?? 0), 0),
    trackCount: matches.length,
  };
};

export const candidateSorts = {
  match: 'Best match',
  quality: 'Best quality',
  speed: 'Fastest',
};

const completeness = (candidate) =>
  candidate.trackCount ? candidate.matchedCount / candidate.trackCount : 0;

const compareBy =
  (...keys) =>
  (a, b) => {
    for (const key of keys) {
      const difference = key(b) - key(a);
      if (difference !== 0) return difference;
    }

    return 0;
  };

const availability = [
  (candidate) => (candidate.locked ? 0 : 1),
  (candidate) => (candidate.user.hasFreeUploadSlot ? 1 : 0),
  (candidate) => candidate.user.uploadSpeed ?? 0,
  (candidate) => -(candidate.user.queueLength ?? 0),
];

const comparers = {
  match: compareBy(
    completeness,
    (candidate) => -candidate.extras.length,
    (candidate) => candidate.quality.rank,
    ...availability,
  ),
  quality: compareBy(
    (candidate) => (candidate.complete ? 1 : 0),
    (candidate) => candidate.quality.rank,
    completeness,
    ...availability,
  ),
  speed: compareBy(
    (candidate) => (candidate.complete ? 1 : 0),
    ...availability,
    completeness,
    (candidate) => candidate.quality.rank,
  ),
};

// groups search responses into album folders and ranks them against the release
export const buildCandidates = ({
  release,
  responses = [],
  sort = 'match',
}) => {
  const groups = new Map();

  for (const response of responses) {
    const user = {
      hasFreeUploadSlot: response.hasFreeUploadSlot,
      queueLength: response.queueLength,
      uploadSpeed: response.uploadSpeed,
      username: response.username,
    };

    const files = [
      ...(response.files ?? []),
      ...(response.lockedFiles ?? []).map((file) => ({
        ...file,
        locked: true,
      })),
    ];

    for (const file of files) {
      const { folder } = albumFolderOf(file.filename);
      const key = `${response.username}\u0000${folder}`;

      if (!groups.has(key)) {
        groups.set(key, { audio: [], folder, key, other: [], user });
      }

      const group = groups.get(key);
      (isAudio(file.filename) ? group.audio : group.other).push(file);
    }
  }

  return [...groups.values()]
    .filter((group) => group.audio.length > 0)
    .map((group) => evaluateCandidate(group, release))
    .filter((candidate) => candidate.matchedCount > 0)
    .sort(comparers[sort] ?? comparers.match);
};

// the words to search Soulseek for; "Various Artists" is left out since folders rarely say so
export const soulseekQuery = (release) => {
  const artist = /^various artists$/iu.test(release?.artist ?? '')
    ? ''
    : release?.artist ?? '';

  return [
    ...new Set(
      `${artist} ${release?.title ?? ''}`
        .toLowerCase()
        .split(/[^\p{L}\p{N}]+/u)
        .filter(Boolean),
    ),
  ].join(' ');
};

const sanitizeSegment = (text) =>
  text
    // eslint-disable-next-line no-control-regex
    .replaceAll(/[\u0000-\u001F"*/:<>?\\|]/gu, '_')
    .replaceAll(/\s+/gu, ' ')
    .trim()
    .replace(/[. ]+$/u, '');

export const destinationFor = (release) => {
  const year = release?.date?.slice(0, 4);
  return sanitizeSegment(
    `${release?.artist ?? 'Unknown Artist'} - ${release?.title ?? 'Unknown Release'}${year ? ` (${year})` : ''}`,
  );
};

// one batch per remote directory, so that disc folders keep files with the same names apart
export const downloadPlan = ({ candidate, includeExtras = true, release }) => {
  const destination = destinationFor(release);
  const files = [
    ...candidate.matches
      .filter((match) => match.file)
      .map((match) => match.file),
    ...(includeExtras ? candidate.other : []),
  ].filter((file) => !file.locked);

  const batches = new Map();
  for (const file of files) {
    const { subfolder } = albumFolderOf(file.filename);
    const target = subfolder
      ? `${destination}/${sanitizeSegment(subfolder)}`
      : destination;
    if (!batches.has(target)) batches.set(target, []);
    batches.get(target).push({ filename: file.filename, size: file.size });
  }

  return [...batches.entries()].map(([target, batchFiles]) => ({
    destination: target,
    files: batchFiles,
  }));
};
