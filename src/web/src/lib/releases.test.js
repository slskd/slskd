import {
  albumFolderOf,
  buildCandidates,
  destinationFor,
  downloadPlan,
  matchFiles,
  parseTrackFilename,
  soulseekQuery,
  summarizeQuality,
} from './releases';

const track = (position, title, seconds, number = String(position)) => ({
  length: seconds * 1_000,
  number,
  position,
  title,
});

const release = {
  artist: 'Aphex Twin',
  date: '1992-11-09',
  media: [
    {
      position: 1,
      tracks: [
        track(1, 'Xtal', 294),
        track(2, 'Tha', 545),
        track(3, 'Pulsewidth', 228),
        track(4, 'Ageispolis', 322),
      ],
    },
  ],
  title: 'Selected Ambient Works 85–92',
};

const flac = (filename, seconds, extra = {}) => ({
  bitDepth: 16,
  filename,
  length: seconds,
  sampleRate: 44_100,
  size: 30_000_000,
  ...extra,
});

describe('parseTrackFilename', () => {
  it.each([
    ['01 - Xtal.flac', { track: 1, words: ['xtal'] }],
    ['01. Xtal.flac', { track: 1, words: ['xtal'] }],
    ['(03) Pulsewidth.mp3', { track: 3, words: ['pulsewidth'] }],
    ['1-02 Tha.flac', { disc: 1, track: 2, words: ['tha'] }],
    ['CD2-05 Something.flac', { disc: 2, track: 5, words: ['something'] }],
    ['A1 Intro.flac', { code: 'a1', words: ['intro'] }],
    ['Aphex Twin - 04 - Ageispolis.flac', { track: 4 }],
    ['Track 07.wav', { track: 7, words: [] }],
    ['Xtal.flac', { words: ['xtal'] }],
  ])('reads %s', (filename, expected) => {
    expect(parseTrackFilename(`music\\album\\${filename}`)).toMatchObject(
      expected,
    );
  });

  it('does not mistake a year in the title for a track number', () => {
    expect(parseTrackFilename('1979.flac').track).toBeUndefined();
  });
});

describe('albumFolderOf', () => {
  it('merges disc folders into the album folder', () => {
    expect(albumFolderOf('music\\Album\\CD2\\01.flac')).toEqual({
      disc: 2,
      folder: 'music\\Album',
      subfolder: 'CD2',
    });
    expect(albumFolderOf('music/Album/Disc 1 - Bonus/01.flac')).toMatchObject({
      disc: 1,
      folder: 'music/Album',
    });
  });

  it('leaves other folders alone', () => {
    expect(albumFolderOf('music\\Album\\01.flac')).toMatchObject({
      disc: undefined,
      folder: 'music\\Album',
    });
  });
});

describe('matchFiles', () => {
  it('matches by title, number and length, and reports extras', () => {
    const files = [
      flac('a\\SAW\\02 - Tha.flac', 545),
      flac('a\\SAW\\01 - Xtal.flac', 293),
      flac('a\\SAW\\03 - Pulsewidth.flac', 228),
      flac('a\\SAW\\05 - Bonus Track.flac', 100),
    ];

    const { extras, matches } = matchFiles({ files, release });

    expect(
      matches.map((match) => match.file?.filename.split('\\').pop()),
    ).toEqual([
      '01 - Xtal.flac',
      '02 - Tha.flac',
      '03 - Pulsewidth.flac',
      undefined,
    ]);
    expect(extras.map((file) => file.filename)).toEqual([
      'a\\SAW\\05 - Bonus Track.flac',
    ]);
  });

  it('ignores the artist and album in file names', () => {
    const files = [
      flac('a\\Aphex Twin - Selected Ambient Works - Xtal.flac', 294),
    ];
    expect(matchFiles({ files, release }).matches[0].file).toBe(files[0]);
  });

  it('matches files named only by number when the length agrees', () => {
    const files = [flac('a\\SAW\\02.flac', 546), flac('a\\SAW\\03.flac', 900)];
    const { matches } = matchFiles({ files, release });

    expect(matches[1].file).toBe(files[0]);
    // the number agrees, but the length is far off
    expect(matches[2].file).toBeUndefined();
  });

  it('rejects a different version of a track', () => {
    const files = [flac('a\\SAW\\01 - Xtal (Remix).flac', 420)];
    expect(matchFiles({ files, release }).matches[0].file).toBeUndefined();
  });

  it('tolerates a typo in a long title', () => {
    const files = [
      flac('a\\SAW\\04 - Ageispolis.flac', 322),
      flac('a\\SAW\\Pulswidth.flac', 229),
    ];
    const { matches } = matchFiles({ files, release });
    expect(matches[2].file).toBe(files[1]);
  });

  it('uses disc folders and disc numbers on multi-disc releases', () => {
    const twoDiscs = {
      artist: 'X',
      media: [
        {
          position: 1,
          tracks: [track(1, 'Alpha', 200), track(2, 'Beta', 210)],
        },
        {
          position: 2,
          tracks: [track(1, 'Gamma', 220), track(2, 'Delta', 230)],
        },
      ],
      title: 'Y',
    };

    const files = [
      flac('a\\Y\\CD1\\01.flac', 200),
      flac('a\\Y\\CD2\\01.flac', 220),
      flac('a\\Y\\CD2\\02 Delta.flac', 230),
      flac('a\\Y\\102 Beta.flac', 210),
    ];

    const { matches } = matchFiles({ files, release: twoDiscs });
    expect(matches.map((match) => match.file)).toEqual([
      files[0],
      files[3],
      files[1],
      files[2],
    ]);
  });
});

describe('summarizeQuality', () => {
  it('labels lossless files by bit depth and sample rate', () => {
    expect(
      summarizeQuality([
        flac('a.flac', 1, { bitDepth: 24, sampleRate: 96_000 }),
        flac('b.flac', 1, { bitDepth: 24, sampleRate: 96_000 }),
      ]).label,
    ).toBe('FLAC 24/96');
    expect(summarizeQuality([flac('a.flac', 1)]).label).toBe('FLAC 16/44.1');
  });

  it('labels lossy files by bitrate', () => {
    expect(summarizeQuality([{ bitRate: 320, filename: 'a.mp3' }]).label).toBe(
      'MP3 320',
    );
    expect(
      summarizeQuality([
        { bitRate: 240, filename: 'a.mp3', isVariableBitRate: true },
        { bitRate: 260, filename: 'b.mp3', isVariableBitRate: true },
      ]).label,
    ).toBe('MP3 ~250 VBR');
  });

  it('ranks lossless above lossy', () => {
    expect(summarizeQuality([flac('a.flac', 1)]).rank).toBeGreaterThan(
      summarizeQuality([{ bitRate: 320, filename: 'a.mp3' }]).rank,
    );
  });
});

const response = (username, files, extra = {}) => ({
  files,
  hasFreeUploadSlot: true,
  lockedFiles: [],
  queueLength: 0,
  uploadSpeed: 1_000_000,
  username,
  ...extra,
});

const complete = (root, extra) =>
  release.media[0].tracks.map((item) =>
    flac(
      `${root}\\0${item.position} - ${item.title}.flac`,
      item.length / 1_000,
      extra,
    ),
  );

describe('buildCandidates', () => {
  it('groups by user and folder and ranks complete folders first', () => {
    const candidates = buildCandidates({
      release,
      responses: [
        response('partial', complete('m\\SAW').slice(0, 2)),
        response('full', [
          ...complete('m\\SAW'),
          { filename: 'm\\SAW\\cover.jpg', size: 1 },
        ]),
        response('unrelated', [flac('m\\Other\\01 - Nothing.flac', 10)]),
      ],
    });

    expect(candidates.map((candidate) => candidate.user.username)).toEqual([
      'full',
      'partial',
    ]);
    expect(candidates[0]).toMatchObject({
      complete: true,
      matchedCount: 4,
      trackCount: 4,
    });
    expect(candidates[0].other).toHaveLength(1);
  });

  it('prefers higher quality among complete folders, then faster users', () => {
    const candidates = buildCandidates({
      release,
      responses: [
        response(
          'slow-hires',
          complete('m\\SAW', { bitDepth: 24, sampleRate: 96_000 }),
          {
            uploadSpeed: 10,
          },
        ),
        response('fast-cd', complete('m\\SAW'), { uploadSpeed: 5_000_000 }),
        response('faster-cd', complete('m\\SAW'), { uploadSpeed: 9_000_000 }),
      ],
    });

    expect(candidates.map((candidate) => candidate.user.username)).toEqual([
      'slow-hires',
      'faster-cd',
      'fast-cd',
    ]);

    const bySpeed = buildCandidates({
      release,
      responses: [
        response(
          'slow-hires',
          complete('m\\SAW', { bitDepth: 24, sampleRate: 96_000 }),
          {
            uploadSpeed: 10,
          },
        ),
        response('fast-cd', complete('m\\SAW'), { uploadSpeed: 5_000_000 }),
      ],
      sort: 'speed',
    });

    expect(bySpeed[0].user.username).toBe('fast-cd');
  });
});

describe('soulseekQuery', () => {
  it('uses the artist and title words', () => {
    expect(soulseekQuery(release)).toBe(
      'aphex twin selected ambient works 85 92',
    );
  });

  it('leaves out Various Artists', () => {
    expect(soulseekQuery({ artist: 'Various Artists', title: 'Now 42' })).toBe(
      'now 42',
    );
  });

  it('keeps accents, since Soulseek matches them literally', () => {
    expect(soulseekQuery({ artist: 'Björk', title: 'Homogenic' })).toBe(
      'björk homogenic',
    );
  });
});

describe('downloads', () => {
  it('names the destination after the release, without unsafe characters', () => {
    expect(destinationFor(release)).toBe(
      'Aphex Twin - Selected Ambient Works 85–92 (1992)',
    );
    expect(destinationFor({ artist: 'AC/DC', title: 'What?' })).toBe(
      'AC_DC - What_',
    );
  });

  it('plans one batch per disc folder, with extras and without locked files', () => {
    const [candidate] = buildCandidates({
      release: {
        artist: 'X',
        media: [
          { position: 1, tracks: [track(1, 'Alpha', 200)] },
          { position: 2, tracks: [track(1, 'Gamma', 220)] },
        ],
        title: 'Y',
      },
      responses: [
        {
          files: [
            flac('m\\Y\\CD1\\01 Alpha.flac', 200),
            flac('m\\Y\\CD2\\01 Gamma.flac', 220),
            { filename: 'm\\Y\\folder.jpg', size: 5 },
          ],
          lockedFiles: [{ filename: 'm\\Y\\secret.log', size: 1 }],
          username: 'u',
        },
      ],
    });

    const plan = downloadPlan({
      candidate,
      release: { artist: 'X', title: 'Y' },
    });

    expect(plan).toEqual([
      {
        destination: 'X - Y/CD1',
        files: [{ filename: 'm\\Y\\CD1\\01 Alpha.flac', size: 30_000_000 }],
      },
      {
        destination: 'X - Y/CD2',
        files: [{ filename: 'm\\Y\\CD2\\01 Gamma.flac', size: 30_000_000 }],
      },
      {
        destination: 'X - Y',
        files: [{ filename: 'm\\Y\\folder.jpg', size: 5 }],
      },
    ]);

    expect(
      downloadPlan({
        candidate,
        includeExtras: false,
        release: { artist: 'X', title: 'Y' },
      }).flatMap((batch) => batch.files),
    ).toHaveLength(2);
  });
});

// cases seen on the network
describe('real folders', () => {
  it('matches a folder numbered per vinyl side, from a master a few seconds shorter', () => {
    const okComputer = {
      artist: 'Radiohead',
      media: [
        {
          position: 1,
          tracks: [
            track(7, 'Fitter Happier', 117),
            track(8, 'Electioneering', 231),
            track(9, 'Climbing Up the Walls', 285),
            track(12, 'The Tourist', 325),
          ],
        },
      ],
      title: 'OK Computer',
    };

    const files = [
      { bitRate: 320, filename: 'm\\OKC\\01. Fitter Happier.mp3', length: 113 },
      { bitRate: 320, filename: 'm\\OKC\\02. Electioneering.mp3', length: 223 },
      {
        bitRate: 320,
        filename: 'm\\OKC\\03. Climbing Up the Walls.mp3',
        length: 276,
      },
      { bitRate: 320, filename: 'm\\OKC\\06. The Tourist.mp3', length: 316 },
    ];

    const { extras, matches } = matchFiles({ files, release: okComputer });
    expect(matches.map((match) => match.file)).toEqual(files);
    expect(extras).toEqual([]);
  });

  it('labels and ranks FLAC files that report no bit depth or sample rate as lossless', () => {
    const quality = summarizeQuality([
      { bitRate: 784, filename: 'a.flac' },
      { bitRate: 901, filename: 'b.flac' },
    ]);

    expect(quality.label).toBe('FLAC');
    expect(quality.rank).toBeGreaterThan(
      summarizeQuality([{ bitRate: 320, filename: 'a.mp3' }]).rank,
    );
  });
});
