import {
  parseReleaseInput,
  searchFor,
  searchReleases,
} from '../../lib/musicbrainz';
import { getErrorMessage } from '../../lib/util';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import ReleaseIcon from '../Shared/ReleaseIcon';
import CoverThumbnail from './CoverThumbnail';
import React, { useEffect, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { Button, Segment, Table } from 'semantic-ui-react';

const pageSize = 25;

// results by query, so going back to a search doesn't ask MusicBrainz again
const cache = new Map();

const ReleaseSearch = ({ base, query }) => {
  const history = useHistory();
  const [result, setResult] = useState(() => cache.get(query));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  useEffect(() => {
    if (!query) {
      setResult(undefined);
      return undefined;
    }

    if (cache.has(query)) {
      setResult(cache.get(query));
      setError(undefined);
      return undefined;
    }

    let cancelled = false;

    const search = async () => {
      setLoading(true);
      setError(undefined);

      try {
        const found = await searchReleases({
          ...searchFor(parseReleaseInput(query)),
          limit: pageSize,
        });

        if (!cancelled) {
          cache.set(query, found);
          setResult(found);
        }
      } catch (searchError) {
        if (!cancelled) setError(searchError);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    search();

    return () => {
      cancelled = true;
    };
  }, [query]);

  const loadMore = async () => {
    setLoading(true);

    try {
      const more = await searchReleases({
        ...searchFor(parseReleaseInput(query)),
        limit: pageSize,
        offset: result.releases.length,
      });

      const combined = {
        ...more,
        releases: [...result.releases, ...more.releases],
      };
      cache.set(query, combined);
      setResult(combined);
    } catch (moreError) {
      setError(moreError);
    } finally {
      setLoading(false);
    }
  };

  if (!query) {
    return (
      <PlaceholderSegment
        caption="Search MusicBrainz for a release, then find it on Soulseek"
        icon={<ReleaseIcon />}
      />
    );
  }

  if (error && !result) {
    return <ErrorSegment caption={getErrorMessage(error)} />;
  }

  if (!result) {
    return <LoaderSegment />;
  }

  if (result.releases.length === 0) {
    return (
      <PlaceholderSegment
        caption="MusicBrainz found no releases. Try Artist - Album."
        icon={<ReleaseIcon />}
      />
    );
  }

  return (
    <Segment
      className="release-results"
      raised
    >
      <div className="release-results-count">
        {result.count.toLocaleString()} release{result.count === 1 ? '' : 's'}{' '}
        on MusicBrainz
      </div>
      <Table
        className="release-results-table"
        selectable
        unstackable
      >
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell className="release-results-cover" />
            <Table.HeaderCell>Release</Table.HeaderCell>
            <Table.HeaderCell>Date</Table.HeaderCell>
            <Table.HeaderCell className="release-results-optional">
              Country
            </Table.HeaderCell>
            <Table.HeaderCell>Format</Table.HeaderCell>
            <Table.HeaderCell>Tracks</Table.HeaderCell>
            <Table.HeaderCell className="release-results-optional">
              Label
            </Table.HeaderCell>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {result.releases.map((release) => (
            <Table.Row
              className="release-results-row"
              key={release.id}
              onClick={() => history.push(`${base}/${release.id}`, { query })}
            >
              <Table.Cell className="release-results-cover">
                <CoverThumbnail release={release} />
              </Table.Cell>
              <Table.Cell>
                <a
                  className="release-results-title"
                  href={`${base}/${release.id}`}
                  onClick={(event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    history.push(`${base}/${release.id}`, { query });
                  }}
                >
                  {release.title}
                </a>
                {release.disambiguation && (
                  <span className="release-disambiguation">
                    {' '}
                    ({release.disambiguation})
                  </span>
                )}
                <div className="release-results-artist">
                  {release.artist}
                  {release.type && ` · ${release.type}`}
                  {release.status &&
                    release.status !== 'Official' &&
                    ` · ${release.status}`}
                </div>
              </Table.Cell>
              <Table.Cell>{release.date}</Table.Cell>
              <Table.Cell className="release-results-optional">
                {release.country}
              </Table.Cell>
              <Table.Cell>{release.format}</Table.Cell>
              <Table.Cell>{release.trackCount || ''}</Table.Cell>
              <Table.Cell className="release-results-optional">
                {release.label}
                {release.catalogNumber && (
                  <div className="release-results-catalog">
                    {release.catalogNumber}
                  </div>
                )}
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
      {error && <div className="release-error">{getErrorMessage(error)}</div>}
      {result.releases.length < result.count && (
        <Button
          fluid
          loading={loading}
          onClick={loadMore}
        >
          Show more ({(result.count - result.releases.length).toLocaleString()}{' '}
          remaining)
        </Button>
      )}
    </Segment>
  );
};

export default ReleaseSearch;
