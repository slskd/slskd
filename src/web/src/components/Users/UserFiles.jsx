import '../Browse/Browse.css';
import { urlBase } from '../../config';
import {
  buildDirectoryTree,
  findDirectoryByPath,
  formatBrowseSummary,
  processBrowseResponse,
} from '../../lib/browse';
import * as users from '../../lib/users';
import DirectoryTree from '../Browse/DirectoryTree';
import Selection from '../Browse/Selection';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import { getErrorMessage } from './UserProfile';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { Button, Icon, Loader, Message } from 'semantic-ui-react';

// browse responses can be large and slow to fetch; keep the last few so
// flipping between users and tabs doesn't re-request them
const cache = new Map();
const cacheLimit = 5;

const remember = (username, value) => {
  cache.delete(username);
  cache.set(username, value);

  if (cache.size > cacheLimit) {
    cache.delete(cache.keys().next().value);
  }
};

const UserFiles = ({ username }) => {
  const history = useHistory();
  const treeRef = useRef();
  // a slow browse may finish after the panel has moved on to another user
  const currentUsername = useRef(username);
  currentUsername.current = username;
  const [result, setResult] = useState(() => cache.get(username));
  const [status, setStatus] = useState(() =>
    cache.has(username) ? 'complete' : 'idle',
  );
  const [error, setError] = useState();
  const [progress, setProgress] = useState(0);
  const [selected, setSelected] = useState(null);

  const renderDirectoryAction = useCallback(
    (directory) => (
      <Icon
        link
        name="share"
        onClick={() => treeRef.current?.navigateToDirectory(directory.name)}
        title="Navigate to this directory"
      />
    ),
    [],
  );

  const browse = useCallback(async () => {
    setStatus('loading');
    setError(undefined);
    setProgress(0);
    setSelected(null);

    const poll = setInterval(async () => {
      try {
        const response = await users.getBrowseStatus({ username });
        setProgress(response.data?.percentComplete ?? 0);
      } catch {
        // not tracked until the transfer starts
      }
    }, 500);

    try {
      const processed = processBrowseResponse(await users.browse({ username }));
      const next = {
        ...processed,
        fetchedAt: new Date(),
        tree: buildDirectoryTree(processed),
      };

      remember(username, next);

      if (currentUsername.current !== username) {
        return;
      }

      setResult(next);
      setStatus('complete');
    } catch (browseError) {
      if (currentUsername.current !== username) {
        return;
      }

      setError(getErrorMessage(browseError));
      setStatus('error');
    } finally {
      clearInterval(poll);
    }
  }, [username]);

  useEffect(() => {
    const cached = cache.get(username);
    setSelected(null);

    if (cached) {
      setResult(cached);
      setStatus('complete');
    } else {
      setResult(undefined);
      browse();
    }
  }, [browse, username]);

  if (status === 'loading' || status === 'idle') {
    return (
      <Loader
        active
        className="user-files-loader"
        inline="centered"
      >
        Browsing {username}… {Math.round(progress)}%
      </Loader>
    );
  }

  if (status === 'error') {
    return (
      <Message
        className="user-files-message"
        negative
      >
        <Message.Header>Couldn&apos;t browse {username}</Message.Header>
        <p>{error}</p>
        <Button
          content="Try Again"
          icon="refresh"
          onClick={browse}
          size="small"
        />
      </Message>
    );
  }

  const { info, separator, tree } = result;
  const selectedNode = selected ? findDirectoryByPath(selected, tree) : null;

  return (
    <div className="user-files">
      <div className="user-files-toolbar">
        <span className="user-profile-muted">{formatBrowseSummary(info)}</span>
        <Button.Group size="mini">
          <Button
            icon="refresh"
            onClick={browse}
            title="Browse again"
          />
          <Button
            icon="external alternate"
            onClick={() =>
              history.push(`${urlBase}/browse`, { user: username })
            }
            title="Open in the Browse page"
          />
        </Button.Group>
      </div>
      {tree.length === 0 ? (
        <PlaceholderSegment
          caption="User is not sharing any files"
          icon="folder open"
          size="small"
        />
      ) : (
        <>
          <DirectoryTree
            onSelect={(_, value) => setSelected(value.name)}
            ref={treeRef}
            selectedDirectoryName={selected}
            tree={tree}
          />
          {selectedNode ? (
            <Selection
              directorySuffix={renderDirectoryAction}
              key={`${username}\u0000${selected}`}
              locked={selectedNode.locked}
              name={selected}
              node={selectedNode}
              onClose={() => setSelected(null)}
              separator={separator}
              username={username}
            />
          ) : (
            <p className="user-profile-muted user-files-hint">
              Select a folder to see its files and download them.
            </p>
          )}
        </>
      )}
    </div>
  );
};

export default UserFiles;
