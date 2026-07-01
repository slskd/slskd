/* eslint-disable promise/prefer-await-to-then, unicorn/prevent-abbreviations */
import './Browse.css';
import * as users from '../../lib/users';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import { buildBrowseUrl, decodeBrowseParams } from './browseRoutes';
import Directory from './Directory';
import DirectoryTree from './DirectoryTree';
import React, { Component } from 'react';
import { withRouter } from 'react-router-dom';
import { Card, Icon, Input, Loader, Segment } from 'semantic-ui-react';

const initialState = {
  browseError: undefined,
  browseState: 'idle',
  browseStatus: 0,
  directoryError: undefined,
  directoryFilesByName: {},
  directoryState: 'idle',
  info: {
    directories: 0,
    files: 0,
    lockedDirectories: 0,
    lockedFiles: 0,
  },
  interval: undefined,
  selectedDirectory: {},
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
    this.loadState();
    this.setState(
      {
        interval: window.setInterval(this.fetchStatus, 500),
      },
      () => {
        this.syncFromRoute();
        this.saveState();
      },
    );

    document.addEventListener('keyup', this.keyUp, false);
  }

  componentDidUpdate(previousProps) {
    if (this.props.location.pathname !== previousProps.location.pathname) {
      this.syncFromRoute();
    }
  }

  componentWillUnmount() {
    clearInterval(this.state.interval);
    this.setState({ interval: undefined });
    document.removeEventListener('keyup', this.keyUp, false);
  }

  getRouteState = () =>
    decodeBrowseParams(this.props.match.params, this.state.separator);

  syncFromRoute = () => {
    const { directory, username } = this.getRouteState();

    if (!username) {
      this.setState(
        (previousState) => ({
          ...initialState,
          interval: previousState.interval,
        }),
        () => this.saveState(),
      );
      return;
    }

    if (username !== this.state.username || this.state.browseState === 'idle') {
      this.setState(
        (previousState) => ({
          ...initialState,
          interval: previousState.interval,
          username,
        }),
        () => this.browse(username, directory),
      );
      return;
    }

    if (directory && directory !== this.state.selectedDirectory.name) {
      this.selectDirectoryByName(directory);
    } else if (!directory && this.state.selectedDirectory.name) {
      this.setState({
        directoryError: undefined,
        directoryState: 'idle',
        selectedDirectory: {},
      });
    }
  };

  browse = (usernameOverride, directoryToSelect) => {
    const username = usernameOverride || this.inputtext.inputRef.current.value;

    if (!usernameOverride) {
      this.props.history.push(buildBrowseUrl({ username }));
      return;
    }

    this.setState(
      {
        browseError: undefined,
        browseState: 'pending',
        directoryError: undefined,
        directoryFilesByName: {},
        directoryState: 'idle',
        selectedDirectory: {},
        username,
      },
      () => {
        users
          .browseIndex({ username })
          .then((response) => {
            const { directories } = response;
            const { lockedDirectories } = response;

            let separator;
            const allDirectories = directories.concat(
              lockedDirectories.map((d) => ({ ...d, locked: true })),
            );

            for (const directory of allDirectories) {
              if (!separator) {
                if (directory.name.includes('\\')) separator = '\\';
                else if (directory.name.includes('/')) separator = '/';
              }
            }

            separator = separator || '\\';

            this.setState(
              {
                browseError: undefined,
                browseState: 'complete',
                info: response.info,
                separator,
                tree: this.getDirectoryTree({
                  directories: allDirectories,
                  separator,
                }),
              },
              () => {
                this.saveState();
                if (directoryToSelect) {
                  this.selectDirectoryByName(directoryToSelect);
                }
              },
            );
          })
          .catch((error) =>
            this.setState({ browseError: error, browseState: 'error' }),
          );
      },
    );
  };

  clear = () => {
    this.setState(
      (previousState) => ({
        ...initialState,
        interval: previousState.interval,
      }),
      () => {
        localStorage.removeItem('soulseek-example-browse-state');
        this.props.history.push(buildBrowseUrl({}));
        this.inputtext.focus();
      },
    );
  };

  keyUp = (event) => (event.key === 'Escape' ? this.clear() : '');

  saveState = () => {
    this.inputtext.inputRef.current.value = this.state.username;
    this.inputtext.inputRef.current.disabled =
      this.state.browseState !== 'idle' &&
      this.state.browseState !== 'complete';

    localStorage.setItem(
      'soulseek-example-browse-state',
      JSON.stringify({
        selectedDirectoryName: this.state.selectedDirectory.name || '',
        separator: this.state.separator,
        username: this.state.username,
      }),
    );
  };

  loadState = () => {
    const routeState = this.getRouteState();
    if (routeState.username) {
      return;
    }

    try {
      const saved = JSON.parse(
        localStorage.getItem('soulseek-example-browse-state') || '{}',
      );

      if (saved.username) {
        const stateUpdate = {};
        if (saved.separator) {
          stateUpdate.separator = saved.separator;
        }

        this.setState(stateUpdate, () => {
          this.props.history.replace(
            buildBrowseUrl({
              directory: saved.selectedDirectoryName,
              username: saved.username,
            }),
          );
        });
      }
    } catch (error) {
      console.error(error);
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

  getDirectoryTree = ({ directories, separator }) => {
    if (directories.length === 0 || directories[0].name === undefined) {
      return [];
    }

    // Optimise this process so we only:
    // - loop through all directories once
    // - do the split once
    // - future look ups are done from the Map
    const depthMap = new Map();
    for (const d of directories) {
      const directoryDepth = d.name.split(separator).length;
      if (!depthMap.has(directoryDepth)) {
        depthMap.set(directoryDepth, []);
      }

      depthMap.get(directoryDepth).push(d);
    }

    const depth = Math.min(...Array.from(depthMap.keys()));

    return depthMap
      .get(depth)
      .map((directory) =>
        this.getChildDirectories(depthMap, directory, separator, depth + 1),
      );
  };

  getChildDirectories = (depthMap, root, separator, depth) => {
    if (!depthMap.has(depth)) {
      return { ...root, children: [] };
    }

    const children = depthMap
      .get(depth)
      .filter((d) => d.name.startsWith(root.name));

    return {
      ...root,
      children: children.map((c) =>
        this.getChildDirectories(depthMap, c, separator, depth + 1),
      ),
    };
  };

  findDirectoryByName = (directories, directoryName) => {
    for (const directory of directories || []) {
      if (directory.name === directoryName) {
        return directory;
      }

      const child = this.findDirectoryByName(directory.children, directoryName);
      if (child) {
        return child;
      }
    }

    return undefined;
  };

  selectDirectoryByName = (directoryName) => {
    const directory = this.findDirectoryByName(this.state.tree, directoryName);

    if (!directory) {
      this.setState({
        directoryError: `Directory '${directoryName}' was not found in this browse response.`,
        directoryState: 'error',
        selectedDirectory: { name: directoryName },
      });
      return;
    }

    this.selectDirectory(directory, { updateHistory: false });
  };

  loadDirectoryFiles = (directory) => {
    if (this.state.directoryFilesByName[directory.name]) {
      this.setState({ directoryError: undefined, directoryState: 'complete' });
      return;
    }

    const directoryName = directory.name;
    this.setState(
      { directoryError: undefined, directoryState: 'pending' },
      async () => {
        try {
          const allDirectories = await users.getDirectoryContents({
            directory: directoryName,
            username: this.state.username,
          });

          if (this.state.selectedDirectory.name !== directoryName) {
            return;
          }

          const rootDirectory = allDirectories?.[0];

          if (!rootDirectory) {
            throw new Error('No directories were included in the response');
          }

          this.setState((previousState) => ({
            directoryFilesByName: {
              ...previousState.directoryFilesByName,
              [directoryName]: (rootDirectory.files || []).map((file) => ({
                ...file,
                filename: `${directoryName}${previousState.separator}${file.filename}`,
              })),
            },
            directoryState: 'complete',
          }));
        } catch (error) {
          if (this.state.selectedDirectory.name !== directoryName) {
            return;
          }

          console.error(error);
          this.setState({
            directoryError: error?.response?.data ?? error?.message ?? error,
            directoryState: 'error',
          });
        }
      },
    );
  };

  selectDirectory = (directory, { updateHistory = true } = {}) => {
    this.setState(
      {
        directoryError: undefined,
        selectedDirectory: { ...directory, children: [] },
      },
      () => {
        if (updateHistory) {
          this.props.history.push(
            buildBrowseUrl({
              directory: directory.name,
              username: this.state.username,
            }),
          );
        }

        this.loadDirectoryFiles(directory);
        this.saveState();
      },
    );
  };

  handleDeselectDirectory = () => {
    this.props.history.push(buildBrowseUrl({ username: this.state.username }));
  };

  render() {
    const {
      browseError,
      browseState,
      browseStatus,
      directoryError,
      directoryFilesByName,
      directoryState,
      info,
      selectedDirectory,
      tree,
      username,
    } = this.state;
    const { locked, name } = selectedDirectory;
    const pending = browseState === 'pending';

    const emptyTree = !(tree && tree.length > 0);

    const files = directoryFilesByName[name] || [];

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
                  <PlaceholderSegment
                    caption="User is not sharing any files"
                    icon="folder open"
                  />
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
                      <Segment className="browse-folderlist">
                        <DirectoryTree
                          onSelect={(_, value) => this.selectDirectory(value)}
                          selectedDirectoryName={name}
                          tree={tree}
                        />
                      </Segment>
                    </Card.Content>
                  </Card>
                )}
                {name && (
                  <Directory
                    error={directoryError}
                    files={files}
                    loading={directoryState === 'pending'}
                    locked={locked}
                    marginTop={-20}
                    name={name}
                    onClose={this.handleDeselectDirectory}
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
