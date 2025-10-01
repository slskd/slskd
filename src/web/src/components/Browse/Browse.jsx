/* eslint-disable promise/prefer-await-to-then */
import './Browse.css';
import * as transfers from '../../lib/transfers';
import * as users from '../../lib/users';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import Directory from './Directory';
import DirectoryTree from './DirectoryTree';
import * as lzString from 'lz-string';
import React, { Component } from 'react';
import { withRouter } from 'react-router-dom';
import { Button, Card, Icon, Input, Loader, Segment } from 'semantic-ui-react';

const initialState = {
  browseError: undefined,
  browseState: 'idle',
  browseStatus: 0,
  downloadRequest: undefined,
  info: {
    directories: 0,
    files: 0,
    lockedDirectories: 0,
    lockedFiles: 0,
  },
  interval: undefined,
  selectedDirectory: {},
  selectedFiles: [],
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
    this.loadState();
    this.setState(
      {
        interval: window.setInterval(this.fetchStatus, 500),
      },
      () => this.saveState(),
    );
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

  getRequestsRecursively = (separator, directory, path) => {
    const dirname = directory.name.split(separator).pop();
    const relpath = path ? `${path}${separator}${dirname}` : dirname;
    let requests =
      directory.files.map((f) => ({
        filename: `${directory.name}${separator}${f.filename}|${relpath}${separator}${f.filename}`,
        size: f.size,
      })) || [];
    for (const child of directory.children || []) {
      requests = requests.concat(
        this.getRequestsRecursively(separator, child, relpath),
      );
    }

    return requests;
  };

  handleDownloadRecursively = (username, separator, selectedDirectory) => {
    this.setState({ downloadRequest: 'inProgress' }, async () => {
      try {
        const requests =
          this.getRequestsRecursively(separator, selectedDirectory, '') || [];
        await transfers.download({ files: requests, username });

        this.setState({ downloadRequest: 'complete' });
      } catch (error) {
        this.setState({
          downloadError: error.response,
          downloadRequest: 'error',
        });
      }
    });
  };

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
      this.saveState();
      this.inputtext.focus();
    });
  };

  keyUp = (event) => (event.key === 'Escape' ? this.clear() : '');

  saveState = () => {
    this.inputtext.inputRef.current.value = this.state.username;
    this.inputtext.inputRef.current.disabled =
      this.state.browseState !== 'idle';

    const storeToLocalStorage = () => {
      try {
        localStorage.setItem(
          'soulseek-example-browse-state',
          lzString.compress(JSON.stringify(this.state)),
        );
      } catch (error) {
        console.error(error);
      }
    };

    // Shifting the compression and safe out of the current render loop to speed up responsiveness
    // requestIdleCallback is not supported in Safari hence we push to next tick using Promise.resolve
    if (window.requestIdleCallback) {
      window.requestIdleCallback(storeToLocalStorage);
    } else {
      Promise.resolve().then(storeToLocalStorage);
    }
  };

  loadState = () => {
    this.setState(
      (!this.props.location.state?.user &&
        JSON.parse(
          lzString.decompress(
            localStorage.getItem('soulseek-example-browse-state') || '',
          ),
        )) ||
        initialState,
    );
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

  selectDirectory = (directory) => {
    this.setState({ selectedDirectory: directory }, () => this.saveState());
  };

  handleDeselectDirectory = () => {
    this.setState({ selectedDirectory: initialState.selectedDirectory }, () =>
      this.saveState(),
    );
  };

  render() {
    const {
      browseError,
      browseState,
      browseStatus,
      downloadRequest,
      info,
      selectedDirectory,
      separator,
      tree,
      username,
    } = this.state;
    const { locked, name } = selectedDirectory;
    const pending = browseState === 'pending';

    const emptyTree = !(tree && tree.length > 0);

    const files = (selectedDirectory.files || []).map((f) => ({
      ...f,
      filename: `${name}${separator}${f.filename}`,
    }));

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
                    caption="No user share to display"
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
                      {name && (
                        <Button
                          color="green"
                          content="Download Folder"
                          disabled={downloadRequest === 'inProgress'}
                          icon="download"
                          onClick={() =>
                            this.handleDownloadRecursively(
                              username,
                              separator,
                              selectedDirectory,
                            )
                          }
                        />
                      )}
                    </Card.Content>
                  </Card>
                )}
                {name && (
                  <Directory
                    files={files}
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
