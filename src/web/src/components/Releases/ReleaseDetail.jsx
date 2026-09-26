import {
  formatDuration,
  getRelease,
  getSaved,
  releaseUrl,
  saveRelease,
} from '../../lib/musicbrainz';
import {
  buildCandidates,
  candidateSorts,
  soulseekQuery,
} from '../../lib/releases';
import {
  create,
  filterResponse,
  getDefaultFilter,
  getResponses,
  getStatus,
  parseFiltersFromString,
  stop,
} from '../../lib/searches';
import { getDirectoryContents } from '../../lib/users';
import { getErrorMessage } from '../../lib/util';
import SearchFilters from '../Search/Filters/SearchFilters';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import CoverThumbnail from './CoverThumbnail';
import ReleaseCandidate from './ReleaseCandidate';
import React, { useEffect, useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Dropdown,
  Icon,
  Input,
  Segment,
  Table,
} from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const Tracklist = ({ release }) => {
  const multiDisc = release.media.length > 1;

  return (
    <Table
      className="release-tracklist"
      compact
      unstackable
      very
    >
      <Table.Body>
        {release.media.map((medium) => (
          <React.Fragment key={medium.position}>
            {multiDisc && (
              <Table.Row className="release-tracklist-medium">
                <Table.Cell colSpan={3}>
                  {medium.format ?? 'Medium'} {medium.position}
                  {medium.title && ` · ${medium.title}`}
                </Table.Cell>
              </Table.Row>
            )}
            {medium.tracks.map((track) => (
              <Table.Row key={`${medium.position}-${track.position}`}>
                <Table.Cell className="release-tracklist-number">
                  {track.number}
                </Table.Cell>
                <Table.Cell>
                  {track.title}
                  {track.artist && track.artist !== release.artist && (
                    <span className="release-track-artist">
                      {' '}
                      · {track.artist}
                    </span>
                  )}
                </Table.Cell>
                <Table.Cell className="release-tracklist-length">
                  {formatDuration(track.length)}
                </Table.Cell>
              </Table.Row>
            ))}
          </React.Fragment>
        ))}
      </Table.Body>
    </Table>
  );
};

const ReleaseHeader = ({ release }) => {
  const [showTracklist, setShowTracklist] = useState(true);

  return (
    <Segment
      className="release-header"
      raised
    >
      <CoverThumbnail
        release={release}
        size={500}
      />
      <div className="release-header-info">
        <h2 className="release-title">
          {release.title}
          {release.disambiguation && (
            <span className="release-disambiguation">
              {' '}
              ({release.disambiguation})
            </span>
          )}
        </h2>
        <div className="release-artist">{release.artist}</div>
        <div className="release-facts">
          {[
            release.type,
            release.date,
            release.country,
            release.format,
            `${release.trackCount} track${release.trackCount === 1 ? '' : 's'}`,
            [release.label, release.catalogNumber].filter(Boolean).join(' '),
          ]
            .filter(Boolean)
            .join(' · ')}
        </div>
        <div className="release-links">
          <a
            href={releaseUrl(release.id)}
            rel="noopener noreferrer"
            target="_blank"
          >
            <Icon name="external" />
            MusicBrainz
          </a>
          <button
            className="release-link-button"
            onClick={() => setShowTracklist(!showTracklist)}
            type="button"
          >
            <Icon name={showTracklist ? 'caret up' : 'caret down'} />
            {showTracklist ? 'Hide' : 'Show'} track list
          </button>
        </div>
        {showTracklist && <Tracklist release={release} />}
      </div>
    </Segment>
  );
};

const SearchNotes = ({ disabled, search, searchError, searching, status }) => (
  <>
    {disabled && !search && (
      <div className="release-note">
        Connect to the Soulseek server to search.
      </div>
    )}
    {searchError && (
      <div className="release-error">{getErrorMessage(searchError)}</div>
    )}
    {searching && (
      <div className="release-note">
        <Icon
          loading
          name="circle notch"
        />
        Searching… {status?.responseCount ?? 0} users answered with{' '}
        {(status?.fileCount ?? 0).toLocaleString()} files. Results are ranked
        when the search ends; stop it to rank what has arrived.
      </div>
    )}
  </>
);

const ReleaseDetail = ({ disabled, id }) => {
  const [release, setRelease] = useState(undefined);
  const [error, setError] = useState(undefined);

  const [query, setQuery] = useState('');
  const [search, setSearch] = useState(undefined);
  const [status, setStatus] = useState(undefined);
  const [responses, setResponses] = useState(undefined);
  const [searchError, setSearchError] = useState(undefined);
  const [starting, setStarting] = useState(false);

  // files from "load the whole folder", by username
  const [fetchedFiles, setFetchedFiles] = useState({});
  const [loadingFolders, setLoadingFolders] = useState({});

  const [filters, setFilters] = useState(getDefaultFilter);
  const [sort, setSort] = useState('match');
  const [displayCount, setDisplayCount] = useState(10);
  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const [found, saved] = await Promise.all([
          getRelease({ id }),
          getSaved({ id }).catch(() => undefined),
        ]);
        if (cancelled) return;
        setRelease(found);
        setQuery(saved?.query ?? soulseekQuery(found));
        if (saved?.searchId) {
          setSearch({ id: saved.searchId, query: saved.query });
        }
      } catch (loadError) {
        if (!cancelled) setError(loadError);
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [id]);

  // follow the Soulseek search until it finishes, then fetch its responses
  useEffect(() => {
    if (!search?.id) return undefined;

    let cancelled = false;
    let timer;

    const poll = async () => {
      try {
        const current = await getStatus({ id: search.id });
        if (cancelled) return;
        setStatus(current);

        if (current.isComplete) {
          const found = await getResponses({ id: search.id });
          if (!cancelled) setResponses(found ?? []);
          return;
        }
      } catch (pollError) {
        if (cancelled) return;

        if (pollError?.response?.status === 404) {
          // the search was deleted from the search page
          setSearch(undefined);
          setStatus(undefined);
          return;
        }

        setSearchError(pollError);
      }

      timer = setTimeout(poll, 1_000);
    };

    poll();

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [id, search?.id]);

  const startSearch = async () => {
    const searchText = query.trim();
    if (!searchText) return;

    const next = { id: uuidv4(), query: searchText };

    try {
      setStarting(true);
      setSearchError(undefined);
      await create({ id: next.id, searchText });

      // saved on the server, like the search itself; the track list is left out
      try {
        await saveRelease({
          query: searchText,
          release: { ...release, media: undefined },
          searchId: next.id,
        });
      } catch (saveError) {
        toast.warning(
          `The search started, but the release could not be saved: ${getErrorMessage(saveError)}`,
        );
      }

      setResponses(undefined);
      setStatus(undefined);
      setFetchedFiles({});
      setDisplayCount(10);
      setSearch(next);
    } catch (createError) {
      toast.error(getErrorMessage(createError));
    } finally {
      setStarting(false);
    }
  };

  // lists the folder (and its disc folders) to find files the search didn't return
  const loadFolder = async (candidate) => {
    const { key, user } = candidate;
    const directories = [
      ...new Set([
        candidate.folder,
        ...[...candidate.audio, ...candidate.other].map((file) =>
          file.filename.slice(
            0,
            Math.max(
              file.filename.lastIndexOf('\\'),
              file.filename.lastIndexOf('/'),
            ),
          ),
        ),
      ]),
    ];

    setLoadingFolders((current) => ({ ...current, [key]: true }));

    try {
      const listings = await Promise.all(
        directories.map((directory) =>
          getDirectoryContents({ directory, username: user.username }),
        ),
      );

      const files = listings.flat().flatMap((directory) =>
        (directory?.files ?? []).map((file) => ({
          ...file,
          filename: `${directory.name}\\${file.filename}`,
        })),
      );

      setFetchedFiles((current) => ({
        ...current,
        [user.username]: [...(current[user.username] ?? []), ...files],
      }));
    } catch (folderError) {
      toast.error(`Could not list the folder: ${getErrorMessage(folderError)}`);
    } finally {
      setLoadingFolders((current) => ({ ...current, [key]: false }));
    }
  };

  const candidates = useMemo(() => {
    if (!release || !responses) return [];

    const parsed = parseFiltersFromString(filters);

    const merged = responses.map((response) => {
      const extra = fetchedFiles[response.username];
      if (!extra) return response;

      const known = new Set(
        [...(response.files ?? []), ...(response.lockedFiles ?? [])].map(
          (file) => file.filename,
        ),
      );

      return {
        ...response,
        files: [
          ...(response.files ?? []),
          ...extra.filter(
            (file, index, all) =>
              !known.has(file.filename) &&
              all.findIndex((other) => other.filename === file.filename) ===
                index,
          ),
        ],
      };
    });

    return buildCandidates({
      release,
      responses: merged.map((response) =>
        filterResponse({ filters: parsed, response }),
      ),
      sort,
    });
  }, [fetchedFiles, filters, release, responses, sort]);

  if (error) {
    return <ErrorSegment caption={getErrorMessage(error)} />;
  }

  if (!release) {
    return <LoaderSegment />;
  }

  const searching = search && !status?.isComplete;
  const complete = candidates.filter((candidate) => candidate.complete).length;

  return (
    <>
      <ReleaseHeader release={release} />
      <Segment
        className="release-soulseek"
        raised
      >
        <div className="release-soulseek-bar">
          <Input
            action={
              searching ? (
                <Button
                  content="Stop"
                  icon="stop circle"
                  negative
                  onClick={() => stop({ id: search.id })}
                />
              ) : (
                <Button
                  content={search ? 'Search again' : 'Search Soulseek'}
                  disabled={disabled || starting || !query.trim()}
                  icon="search"
                  loading={starting}
                  onClick={startSearch}
                  primary
                />
              )
            }
            className="release-soulseek-query"
            disabled={searching}
            label={{ content: 'Soulseek search' }}
            onChange={(_event, { value }) => setQuery(value)}
            onKeyUp={(event) =>
              event.key === 'Enter' && !searching && startSearch()
            }
            value={query}
          />
        </div>
        <SearchNotes
          disabled={disabled}
          search={search}
          searchError={searchError}
          searching={searching}
          status={status}
        />
        {responses && (
          <>
            <SearchFilters
              onChange={setFilters}
              value={filters}
            />
            <div className="release-candidates-summary">
              <span>
                {candidates.length === 0
                  ? `No folders match this release (${responses.length} users answered).`
                  : `${candidates.length} folder${candidates.length === 1 ? '' : 's'} from ${
                      new Set(
                        candidates.map((candidate) => candidate.user.username),
                      ).size
                    } users; ${complete} complete.`}
              </span>
              <Dropdown
                className="release-candidates-sort"
                inline
                onChange={(_event, { value }) => setSort(value)}
                options={Object.entries(candidateSorts).map(
                  ([value, text]) => ({
                    key: value,
                    text,
                    value,
                  }),
                )}
                value={sort}
              />
            </div>
          </>
        )}
      </Segment>
      {candidates.slice(0, displayCount).map((candidate) => (
        <ReleaseCandidate
          candidate={candidate}
          disabled={disabled}
          key={candidate.key}
          loadingFolder={Boolean(loadingFolders[candidate.key])}
          onLoadFolder={() => loadFolder(candidate)}
          release={release}
          searchId={search?.id}
        />
      ))}
      {candidates.length > displayCount && (
        <Button
          className="showmore-button"
          fluid
          onClick={() => setDisplayCount(displayCount + 10)}
          primary
          size="large"
        >
          Show more ({candidates.length - displayCount} remaining)
        </Button>
      )}
    </>
  );
};

export default ReleaseDetail;
