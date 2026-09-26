import { listSaved, removeAllSaved, removeSaved } from '../../lib/musicbrainz';
import { getAll } from '../../lib/searches';
import { getErrorMessage } from '../../lib/util';
import ClearAllButton from '../Shared/ClearAllButton';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import ReleaseIcon from '../Shared/ReleaseIcon';
import CoverThumbnail from './CoverThumbnail';
import React, { useEffect, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Icon, Segment, Table } from 'semantic-ui-react';

const searchStatus = (search) => {
  if (!search) return <span className="release-muted">search deleted</span>;

  if (!search.isComplete) {
    return (
      <span>
        <Icon
          loading
          name="circle notch"
        />
        searching
      </span>
    );
  }

  return `${search.fileCount.toLocaleString()} files from ${search.responseCount} users`;
};

// the releases searched for on Soulseek, kept like the search list keeps searches
const SavedReleases = ({ base }) => {
  const history = useHistory();
  const [saved, setSaved] = useState(undefined);
  const [searches, setSearches] = useState({});
  const [error, setError] = useState(undefined);

  useEffect(() => {
    const load = async () => {
      try {
        const [releases, allSearches] = await Promise.all([
          listSaved(),
          getAll().catch(() => []),
        ]);

        setSaved(releases);
        setSearches(
          Object.fromEntries(allSearches.map((search) => [search.id, search])),
        );
      } catch (loadError) {
        setError(loadError);
      }
    };

    load();
  }, []);

  const remove = async (id) => {
    try {
      await removeSaved({ id });
      setSaved((current) => current.filter((item) => item.release.id !== id));
    } catch (removeError) {
      toast.error(getErrorMessage(removeError));
    }
  };

  const clearAll = async () => {
    try {
      const count = await removeAllSaved();
      setSaved([]);
      toast.success(`Removed ${count} saved release${count === 1 ? '' : 's'}`);
    } catch (clearError) {
      toast.error(getErrorMessage(clearError));
    }
  };

  if (error) return <ErrorSegment caption={getErrorMessage(error)} />;
  if (!saved) return <LoaderSegment />;

  if (saved.length === 0) {
    return (
      <PlaceholderSegment
        caption="Search MusicBrainz for a release, then find it on Soulseek. Releases you search for are kept here."
        icon={<ReleaseIcon />}
      />
    );
  }

  const open = (id) => history.push(`${base}/${id}`, { from: 'saved' });

  return (
    <Segment
      className="saved-releases"
      raised
    >
      <div className="saved-releases-header">
        <span>
          {saved.length} saved release{saved.length === 1 ? '' : 's'}
        </span>
        <ClearAllButton
          confirm="Remove every saved release, and delete their Soulseek searches?"
          onConfirm={clearAll}
        />
      </div>
      <Table
        className="release-results-table"
        selectable
        unstackable
      >
        <Table.Body>
          {saved.map(({ release, savedAt, searchId }) => (
            <Table.Row
              className="release-results-row"
              key={release.id}
              onClick={() => open(release.id)}
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
                    open(release.id);
                  }}
                >
                  {release.title}
                </a>
                <div className="release-results-artist">
                  {[release.artist, release.date, release.format]
                    .filter(Boolean)
                    .join(' · ')}
                </div>
              </Table.Cell>
              <Table.Cell className="saved-releases-status">
                {searchId ? searchStatus(searches[searchId]) : ''}
              </Table.Cell>
              <Table.Cell className="saved-releases-date release-results-optional">
                {new Date(savedAt).toLocaleString()}
              </Table.Cell>
              <Table.Cell className="saved-releases-action">
                <Icon
                  color="red"
                  link
                  name="trash alternate"
                  onClick={(event) => {
                    event.stopPropagation();
                    remove(release.id);
                  }}
                  title="Remove this release and its Soulseek search"
                />
              </Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
    </Segment>
  );
};

export default SavedReleases;
