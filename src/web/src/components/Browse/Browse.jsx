/* eslint-disable promise/prefer-await-to-then */
import './Browse.css';
import * as users from '../../lib/users';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import DirectoryTree from './DirectoryTree';
import Selection from './Selection';
import React, { Component } from 'react';
import { withRouter } from 'react-router-dom';
import { Card, Icon, Input, Loader, Segment } from 'semantic-ui-react';

const openBrowseDb = () =>
  new Promise((resolve, reject) => {
    const req = indexedDB.open('slskd-browse', 1);
    req.onupgradeneeded = ({ target }) => target.result.createObjectStore('browse');
    req.onsuccess = ({ target }) => resolve(target.result);
    req.onerror = ({ target }) => reject(target.error);
  });

const idbPut = async (key, value) => {
  const db = await openBrowseDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction('browse', 'readwrite');
    tx.objectStore('browse').put(value, key);
    tx.oncomplete = resolve;
    tx.onerror = ({ target }) => reject(target.error);
  });
};

const idbGet = async (key) => {
  const db = await openBrowseDb();
  return new Promise((resolve, reject) => {
    const req = db.transaction('browse').objectStore('browse').get(key);
    req.onsuccess = ({ target }) => resolve(target.result ?? null);
    req.onerror = ({ target }) => reject(target.error);
  });
};

const initialState = {
  browseError: undefined,
  browseLoading: false,
  browseState: 'idle',
  browseStatus: 0,
  directories: [],
  info: {
    directories: 0,
    files: 0,
    lockedDirectories: 0,
    lockedFiles: 0,
  },
  interval: undefined,
  selected: null,
  separator: '\\',
  tree: [],
  username: '',
};

class Browse extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    this.fetchStatus();
    (async () => {
      await this.loadState();
      this.setState(
        { interval: window.setInterval(this.fetchStatus, 500) },
        () => this.saveState(),
      );
    })();
    if (this.props.location.state?.user) {
      this.setState({ username: this.props.location.state.user }, this.browse);
    }

    document.addEventListener('keyup', this.keyUp, false);
  }

  componentWillUnmount() {
    clearInterval(this.state.interval);
    this.setState({ interval: undefined });
    document.removeEventListener('keyup', this.keyUp, false);
  }

  browse = () => {
    const username = this.inputtext.inputRef.current.value;

    this.setState(
      { browseError: undefined, browseState: 'pending', username },
      () => {
        users
          .browse({ username })
          .then((response) => {
            let { directories } = response;
            const { lockedDirectories } = response;

            // we need to know the directory separator. assume it is \ to start
            let separator;

            const directoryCount = directories.length;
            const fileCount = directories.reduce((accumulator, directory) => {
              // examine each directory as we process it to see if it contains \ or /, and set separator accordingly
              if (!separator) {
                if (directory.name.includes('\\')) separator = '\\';
                else if (directory.name.includes('/')) separator = '/';
              }

              return accumulator + directory.fileCount;
            }, 0);

            const lockedDirectoryCount = lockedDirectories.length;
            const lockedFileCount = lockedDirectories.reduce(
              (accumulator, directory) => accumulator + directory.fileCount,
              0,
            );

            directories = directories.concat(
              lockedDirectories.map((d) => ({ ...d, locked: true })),
            );

            this.setState({
              directories,
              info: {
                directories: directoryCount,
                files: fileCount,
                lockedDirectories: lockedDirectoryCount,
                lockedFiles: lockedFileCount,
              },
              separator,
              tree: this.getDirectoryTree({ directories, separator }),
            });
          })
          .then(() =>
            this.setState(
              { browseError: undefined, browseState: 'complete' },
              () => {
                this.saveState();
                setTimeout(() => this.saveTree(), 0);
              },
            ),
          )
          .catch((error) =>
            this.setState({ browseError: error, browseState: 'error' }),
          );
      },
    );
  };

  clear = () => {
    this.setState(initialState, () => {
      this.saveTree();
      this.saveState();
      this.inputtext.focus();
    });
  };

  keyUp = (event) => (event.key === 'Escape' ? this.clear() : '');

  saveTree = async () => {
    try {
      const { directories, info, separator, username } = this.state;
      await idbPut('current', { directories, info, separator, username });
    } catch (error) {
      console.error(error);
    }
  };

  saveState = () => {
    this.inputtext.inputRef.current.value = this.state.username;
    this.inputtext.inputRef.current.disabled =
      this.state.browseState !== 'idle';

    try {
      const { browseError, browseState, info, selected, separator, username } =
        this.state;
      localStorage.setItem(
        'slskd-browse-state',
        JSON.stringify({ browseError, browseState, info, selected, separator, username }),
      );
    } catch (error) {
      console.error(error);
    }
  };

  loadState = async () => {
    if (this.props.location.state?.user) {
      return;
    }

    try {
      const metaStr = localStorage.getItem('slskd-browse-state');
      const meta = metaStr ? JSON.parse(metaStr) : null;

      if (meta?.username) {
        this.setState({ browseLoading: true, username: meta.username }, () => {
          this.inputtext.inputRef.current.value = meta.username;
        });
      }

      const saved = await idbGet('current');

      if (!saved && !meta) {
        this.setState({ browseLoading: false });
        return;
      }

      const directories = saved?.directories ?? [];
      const separator = saved?.separator ?? meta?.separator ?? '\\';
      const tree = directories.length
        ? this.getDirectoryTree({ directories, separator })
        : [];

      this.setState({
        ...(meta ?? {}),
        ...(saved ? { info: saved.info, separator, username: saved.username } : {}),
        browseLoading: false,
        directories,
        tree,
      });
    } catch (error) {
      console.error(error);
      this.setState({ browseLoading: false });
    }
  };

  fetchStatus = () => {
    const { browseState, username } = this.state;
    if (browseState === 'pending') {
      users.getBrowseStatus({ username }).then((response) =>
        this.setState({
          browseStatus: response.data,
        }),
      );
    }
  };

  annotateWithCounts = (node) => {
    const children = (node.children || []).map(this.annotateWithCounts);
    return {
      ...node,
      children,
      totalDirectoryCount:
        children.length +
        children.reduce((sum, c) => sum + c.totalDirectoryCount, 0),
      totalFileCount:
        (node.files || []).length +
        children.reduce((sum, c) => sum + c.totalFileCount, 0),
    };
  };

  getDirectoryTree = ({ directories, separator }) => {
    if (!directories.length || directories[0].name === undefined) {
      return [];
    }

    // Single O(N) pass: group each directory under its parent path.
    // The previous depth-map + startsWith approach was O(N × N/depth_levels)
    // because every parent scanned all entries at the next level.
    const byParent = new Map();
    const nameSet  = new Set();

    for (const d of directories) {
      nameSet.add(d.name);
      const lastSep   = d.name.lastIndexOf(separator);
      const parentKey = lastSep === -1 ? '' : d.name.slice(0, lastSep);
      let bucket = byParent.get(parentKey);
      if (!bucket) { bucket = []; byParent.set(parentKey, bucket); }
      bucket.push(d);
    }

    // Roots: directories whose parent path is not itself a directory in the list.
    const roots = directories.filter((d) => {
      const lastSep   = d.name.lastIndexOf(separator);
      const parentKey = lastSep === -1 ? '' : d.name.slice(0, lastSep);
      return !nameSet.has(parentKey);
    });

    // Build tree nodes recursively, annotating counts in the same pass.
    const buildNode = (dir) => {
      const children = (byParent.get(dir.name) || []).map(buildNode);
      return {
        ...dir,
        children,
        totalDirectoryCount:
          children.length +
          children.reduce((s, c) => s + c.totalDirectoryCount, 0),
        totalFileCount:
          (dir.files?.length ?? 0) +
          children.reduce((s, c) => s + c.totalFileCount, 0),
      };
    };

    return roots.map(buildNode);
  };

  findDirectoryByPath = (path, nodes) => {
    for (const node of nodes) {
      if (node.name === path) {
        return node;
      }

      const found = this.findDirectoryByPath(path, node.children || []);

      if (found) {
        return found;
      }
    }

    return null;
  };

  selectDirectory = (directory) => {
    this.setState(
      {
        selected: {
          directoryName: directory.name,
          files: [],
          subdirectory: directory.children?.[0]?.name ?? null,
        },
      },
      () => this.saveState(),
    );
  };

  handleStateChange = ({ files, subdirectory }) => {
    this.setState(
      (prevState) => ({
        selected: { ...prevState.selected, files, subdirectory },
      }),
      () => this.saveState(),
    );
  };

  handleDirectoryNavigate = (path) => {
    const directory = this.findDirectoryByPath(path, this.state.tree);

    if (directory) {
      this.selectDirectory(directory);
    }
  };

  renderDirectoryAction = (dir) => (
    <Icon
      link
      name="share"
      onClick={() => this.handleDirectoryNavigate(dir.name)}
      title="Navigate to this directory"
    />
  );

  handleDeselectDirectory = () => {
    this.setState({ selected: null }, () => this.saveState());
  };

  render() {
    const {
      browseError,
      browseLoading,
      browseState,
      browseStatus,
      info,
      selected,
      separator,
      tree,
      username,
    } = this.state;
    const selectedDirectory = selected
      ? this.findDirectoryByPath(selected.directoryName, tree)
      : null;
    const locked = selectedDirectory?.locked;
    const pending = browseState === 'pending';

    const emptyTree = !(tree && tree.length > 0);

    return (
      <div className="search-container">
        <Segment
          className="browse-segment"
          raised
        >
          <div className="browse-segment-icon">
            <Icon
              name="folder open"
              size="big"
            />
          </div>
          <Input
            action={
              !pending &&
              (browseState === 'idle'
                ? { icon: 'search', onClick: this.browse }
                : { color: 'red', icon: 'x', onClick: this.clear })
            }
            className="search-input"
            disabled={pending}
            input={
              <input
                data-lpignore="true"
                placeholder="Username"
                type="search"
              />
            }
            loading={pending}
            onKeyUp={(event) => (event.key === 'Enter' ? this.browse() : '')}
            placeholder="Username"
            ref={(input) => (this.inputtext = input)}
            size="big"
          />
        </Segment>
        {pending ? (
          <Loader
            active
            className="search-loader"
            inline="centered"
            size="big"
          >
            Downloaded {Math.round(browseStatus.percentComplete || 0)}% of
            Response
          </Loader>
        ) : (
          <div>
            {browseError ? (
              <span className="browse-error">Failed to browse {username}</span>
            ) : (
              <div className="browse-container">
                {emptyTree ? (
                  browseLoading ? (
                    <Loader
                      active
                      className="search-loader"
                      inline="centered"
                      size="big"
                    >
                      Loading saved results
                    </Loader>
                  ) : (
                    <PlaceholderSegment
                      caption="User is not sharing any files"
                      icon="folder open"
                    />
                  )
                ) : (
                  <Card
                    className="browse-tree-card"
                    raised
                  >
                    <Card.Content>
                      <Card.Header>
                        <Icon
                          color="green"
                          name="circle"
                        />
                        {username}
                      </Card.Header>
                      <Card.Meta className="browse-meta">
                        <span>
                          {`${info.files + info.lockedFiles} files in ${info.directories + info.lockedDirectories} directories (including ${info.lockedFiles} files in ${info.lockedDirectories} locked directories)`}{' '}
                          {/* eslint-disable-line max-len */}
                        </span>
                      </Card.Meta>
                      <div style={{ marginTop: '0.5em' }}>
                        <DirectoryTree
                          onSelect={(_, value) => this.selectDirectory(value)}
                          selectedDirectoryName={selected?.directoryName}
                          tree={tree}
                        />
                      </div>
                    </Card.Content>
                  </Card>
                )}
                {selectedDirectory && (
                  <Selection
                    defaultSelectedFiles={selected.files}
                    defaultSubdirectory={selected.subdirectory}
                    directorySuffix={this.renderDirectoryAction}
                    locked={locked}
                    marginTop={-20}
                    name={selected.directoryName}
                    node={selectedDirectory}
                    onClose={this.handleDeselectDirectory}
                    onStateChange={this.handleStateChange}
                    separator={separator}
                    username={username}
                  />
                )}
              </div>
            )}
          </div>
        )}
      </div>
    );
  }
}

export default withRouter(Browse);
