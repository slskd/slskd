import './Releases.css';
import { parseReleaseInput } from '../../lib/musicbrainz';
import ReleaseIcon from '../Shared/ReleaseIcon';
import ReleaseDetail from './ReleaseDetail';
import ReleaseSearch from './ReleaseSearch';
import SavedReleases from './SavedReleases';
import React, { useEffect, useState } from 'react';
import {
  useHistory,
  useLocation,
  useParams,
  useRouteMatch,
} from 'react-router-dom';
import { Button, Input, Segment } from 'semantic-ui-react';

// the way back from a release or a list of results; back through history when that's where
// the user came from, so the results and scroll position are as they were left
const BackButton = ({ base, id, query }) => {
  const history = useHistory();
  const { state } = useLocation();

  if (id && state?.query) {
    return (
      <Button
        className="releases-back"
        content={`Back to results for “${state.query}”`}
        icon="arrow left"
        onClick={() => history.goBack()}
        size="small"
      />
    );
  }

  if (id || query) {
    return (
      <Button
        className="releases-back"
        content="Saved releases"
        icon="arrow left"
        onClick={() =>
          id && state?.from === 'saved' ? history.goBack() : history.push(base)
        }
        size="small"
      />
    );
  }

  return null;
};

// finds releases on MusicBrainz, then finds and ranks the folders on Soulseek that hold them
const Releases = ({ server }) => {
  const { id } = useParams();
  const history = useHistory();
  const location = useLocation();
  const match = useRouteMatch();

  const base = match.url.replace(/\/releases(?:\/.*)?$/u, '/releases');
  const query = new URLSearchParams(location.search).get('q') ?? '';

  const [text, setText] = useState(query);

  useEffect(() => {
    if (!id) setText(query);
  }, [id, query]);

  const submit = () => {
    const parsed = parseReleaseInput(text);

    if (parsed.type === 'release') {
      history.push(`${base}/${parsed.id}`);
    } else if (text.trim()) {
      history.push(`${base}?q=${encodeURIComponent(text.trim())}`);
    }
  };

  const content = id ? (
    <ReleaseDetail
      disabled={!server?.isConnected}
      id={id}
      key={id}
    />
  ) : query ? (
    <ReleaseSearch
      base={base}
      query={query}
    />
  ) : (
    <SavedReleases base={base} />
  );

  return (
    <div className="releases">
      <Segment
        className="releases-search-segment"
        raised
      >
        <div className="search-segment-icon">
          <ReleaseIcon size="big" />
        </div>
        <Input
          action={
            <Button
              disabled={!text.trim()}
              icon="search"
              onClick={submit}
              title="Search MusicBrainz"
            />
          }
          className="search-input"
          input={
            <input
              aria-label="Release search"
              data-lpignore="true"
              placeholder="Artist - Album, a MusicBrainz link, or a release ID"
              type="search"
            />
          }
          onChange={(_event, { value }) => setText(value)}
          onKeyUp={(event) => event.key === 'Enter' && submit()}
          size="big"
          value={text}
        />
      </Segment>
      <BackButton
        base={base}
        id={id}
        query={query}
      />
      {content}
    </div>
  );
};

export default Releases;
