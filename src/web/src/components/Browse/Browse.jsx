import './Browse.css';
import * as users from '../../lib/users';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import DirectoryTree from './DirectoryTree';
import Selection from './Selection';
import React, { Component } from 'react';
import { withRouter } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Card, Icon, Input, Loader, Segment } from 'semantic-ui-react';

const openBrowseDb = () =>
  new Promise((resolve, reject) => {
    const req = indexedDB.open('slskd-browse', 1);
    req.onupgradeneeded = ({ target }) =>
      target.result.createObjectStore('browse');
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

const formatBrowseSummary = ({
  directories,
  files,
  lockedDirectories,
  lockedFiles,
}) =>
  `${files + lockedFiles} files in ${directories + lockedDirectories} ` +
  `directories (including ${lockedFiles} files in ${lockedDirectories} locked directories)`;

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
    this.directoryTreeRef = React.createRef();
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

  browse = async () => {
    const username = this.inputtext.inputRef.current.value;
    this.setState({ browseError: undefined, browseState: 'pending', username });

    try {
      const response = await users.browse({ username });
      let { directories } = response;
      const { lockedDirectories } = response;

      // detect the path separator from the first directory name we see
      let separator;
      const directoryCount = directories.length;
      const fileCount = directories.reduce((accumulator, directory) => {
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

      this.setState({ browseError: undefined, browseState: 'complete' }, () => {
        this.saveState();
        setTimeout(() => this.saveTree(), 0);
      });
    } catch (error) {
      this.setState({ browseError: error, browseState: 'error' });
    }
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
        JSON.stringify({
          browseError,
          browseState,
          info,
          selected,
          separator,
          username,
        }),
      );
    } catch (error) {
      console.error(error);

      // most likely cause is a huge file selection blowing past
      // localStorage's per-origin quota; the MAX_SELECTED_FILES cap in
      // Selection.jsx should prevent this in practice, but fall back to a
      // clear message rather than silently failing to save
      const isQuotaError =
        error.name === 'QuotaExceededError' || error.code === 22;
      if (isQuotaError) {
        toast.error(
          'Selection is too large to save — try selecting fewer files.',
        );
      }
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
        ...meta,
        ...(saved
          ? { info: saved.info, separator, username: saved.username }
          : {}),
        browseLoading: false,
        directories,
        tree,
      });
    } catch (error) {
      console.error(error);
      this.setState({ browseLoading: false });
    }
  };

  fetchStatus = async () => {
    const { browseState, username } = this.state;
    if (browseState !== 'pending') {
      return;
    }

    const response = await users.getBrowseStatus({ username });
    this.setState({ browseStatus: response.data });
  };

  getDirectoryTree = ({ directories, separator }) => {
    if (!directories.length || directories[0].name === undefined) {
      return [];
    }

    // group each directory under its parent path in a single O(N) pass
    const byParent = new Map();
    const nameSet = new Set();

    for (const d of directories) {
      nameSet.add(d.name);
      const lastSep = d.name.lastIndexOf(separator);
      const parentKey = lastSep === -1 ? '' : d.name.slice(0, lastSep);
      let bucket = byParent.get(parentKey);
      if (!bucket) {
        bucket = [];
        byParent.set(parentKey, bucket);
      }

      bucket.push(d);
    }

    // roots are directories whose parent path isn't itself in the list
    const roots = directories.filter((d) => {
      const lastSep = d.name.lastIndexOf(separator);
      const parentKey = lastSep === -1 ? '' : d.name.slice(0, lastSep);
      return !nameSet.has(parentKey);
    });

    // recursively build the tree, computing file/directory counts along the way
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
    this.directoryTreeRef.current?.navigateToDirectory(path);
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

  renderUsernameBar() {
    const { browseState } = this.state;
    const pending = browseState === 'pending';

    return (
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
    );
  }

  renderTreeAndSelection(selectedDirectory) {
    const { info, selected, separator, tree, username } = this.state;

    return (
      <div className="browse-container">
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
              <span>{formatBrowseSummary(info)}</span>
            </Card.Meta>
            <div className="browse-tree-wrapper">
              <DirectoryTree
                onSelect={(_, value) => this.selectDirectory(value)}
                ref={this.directoryTreeRef}
                selectedDirectoryName={selected?.directoryName}
                tree={tree}
              />
            </div>
          </Card.Content>
        </Card>
        {selectedDirectory && (
          <Selection
            defaultSelectedFiles={selected.files}
            defaultSubdirectory={selected.subdirectory}
            directorySuffix={this.renderDirectoryAction}
            locked={selectedDirectory.locked}
            name={selected.directoryName}
            node={selectedDirectory}
            onClose={this.handleDeselectDirectory}
            onStateChange={this.handleStateChange}
            separator={separator}
            username={username}
          />
        )}
      </div>
    );
  }

  renderResults(selectedDirectory) {
    const { browseError, browseLoading, tree, username } = this.state;

    if (browseError) {
      return <span className="browse-error">Failed to browse {username}</span>;
    }

    const emptyTree = !(tree && tree.length > 0);
    if (emptyTree) {
      return browseLoading ? (
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
      );
    }

    return this.renderTreeAndSelection(selectedDirectory);
  }

  render() {
    const { browseState, browseStatus, selected, tree } = this.state;
    const selectedDirectory = selected
      ? this.findDirectoryByPath(selected.directoryName, tree)
      : null;
    const pending = browseState === 'pending';

    return (
      <div className="search-container">
        {this.renderUsernameBar()}
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
          <div>{this.renderResults(selectedDirectory)}</div>
        )}
      </div>
    );
  }
}

export default withRouter(Browse);
