import React, { Component } from 'react';
import * as lzString from 'lz-string';
import * as users from '../../lib/users';

import './Browse.css';

import DirectoryTree from './DirectoryTree';

import { 
  Segment, 
  Input, 
  Loader,
  Card,
  Icon,
} from 'semantic-ui-react';

import Directory from './Directory';
import PlaceholderSegment from '../Shared/PlaceholderSegment';

const initialState = { 
  username: '', 
  browseState: 'idle', 
  browseStatus: 0,
  browseError: undefined,
  interval: undefined,
  selectedDirectory: {},
  selectedFiles: [],
  tree: [],
  info: {
    directories: 0,
    files: 0,
    lockedDirectories: 0,
    lockedFiles: 0,
  },
};

class Browse extends Component {
  state = initialState;

  browse = () => {
    let username = this.inputtext.inputRef.current.value;

    this.setState({ username , browseState: 'pending', browseError: undefined }, () => {
      users.browse({ username })
        .then(response => {
          let { directories, lockedDirectories } = response;
          
          const directoryCount = directories.length;
          const fileCount = directories.reduce((acc, dir) => acc += dir.fileCount, 0);

          const lockedDirectoryCount = lockedDirectories.length;
          const lockedFileCount = lockedDirectories.reduce((acc, dir) => acc += dir.fileCount, 0);
          
          directories = directories.concat(lockedDirectories.map(d => ({ ...d, locked: true })));
          
          this.setState({ 
            tree: this.getDirectoryTree(directories),
            info: {
              directories: directoryCount,
              files: fileCount,
              lockedDirectories: lockedDirectoryCount,
              lockedFiles: lockedFileCount,
            },
          });
        })
        .then(() => this.setState({ browseState: 'complete', browseError: undefined }, () => {
          this.saveState();
        }))
        .catch(err => this.setState({ browseState: 'error', browseError: err }));
    });
  };

  clear = () => {
    this.setState(initialState, () => {
      this.saveState();
      this.inputtext.focus();
    });
  };

  keyUp = (event) => event.key === 'Escape' ? this.clear() : '';

  onUsernameChange = (event, data) => {
    this.setState({ username: data.value });
  };

  saveState = () => {
    this.inputtext.inputRef.current.value = this.state.username;
    this.inputtext.inputRef.current.disabled = this.state.browseState !== 'idle';

    try {
      localStorage.setItem('soulseek-example-browse-state', lzString.compress(JSON.stringify(this.state)));
    } catch (error) {
      console.log(error);
    }
  };

  loadState = () => {
    this.setState(
      JSON.parse(lzString.decompress(localStorage.getItem('soulseek-example-browse-state') || '')) || initialState);
  };

  componentDidMount = () => {
    this.fetchStatus();
    this.loadState();
    this.setState({ 
      interval: window.setInterval(this.fetchStatus, 500),
    }, () => this.saveState());
    
    document.addEventListener('keyup', this.keyUp, false);
  };
  
  componentWillUnmount = () => {
    clearInterval(this.state.interval);
    this.setState({ interval: undefined });
    document.removeEventListener('keyup', this.keyUp, false);
  };

  fetchStatus = () => {
    const { browseState, username } = this.state;
    if (browseState === 'pending') {
      users.getBrowseStatus({ username })
        .then(response => this.setState({
          browseStatus: response.data,
        }));
    }
  };

  getDirectoryTree = (directories) => {
    if (directories.length === 0 || directories[0].name === undefined) {
      return [];
    }

    const separator = this.sep(directories[0].name);
    const depth = Math.min.apply(null, directories.map(d => d.name.split(separator).length));

    const topLevelDirs = directories
      .filter(d => d.name.split(separator).length === depth);

    return topLevelDirs.map(directory => this.getChildDirectories(directories, directory, separator, depth));
  };

  getChildDirectories = (directories, root, separator, depth) => {
    const children = directories
      .filter(d => d.name !== root.name)
      .filter(d => d.name.split(separator).length === depth + 1)
      .filter(d => d.name.startsWith(root.name));

    return { ...root, children: children.map(c => this.getChildDirectories(directories, c, separator, depth + 1)) };
  };

  selectDirectory = (directory) => {
    this.setState({ selectedDirectory: { ...directory, children: [] }}, () => this.saveState());
  };

  deselectDirectory = () => {
    this.setState({ selectedDirectory: initialState.selectedDirectory }, () => this.saveState());
  };

  sep = (name) => name.includes('\\') ? '\\' : '/';

  render = () => {
    const { browseState, browseStatus, browseError, tree, selectedDirectory, username, info } = this.state;
    const { name, locked } = selectedDirectory;
    const pending = browseState === 'pending';

    const emptyTree = !(tree && tree.length > 0);

    const files = (selectedDirectory.files || [])
      .map(f => ({ ...f, filename: `${name}${this.sep(name)}${f.filename}` }));

    return (
      <div className='search-container'>
        <Segment className='browse-segment' raised>
          <div className="browse-segment-icon"><Icon name="folder open" size="big"/></div>
          <Input
            input={<input placeholder="Username" type="search" data-lpignore="true"></input>}
            size='big'
            ref={input => this.inputtext = input}
            loading={pending}
            disabled={pending}
            className='search-input'
            placeholder="Username"
            action={!pending && (browseState === 'idle'
              ? { icon: 'search', onClick: this.browse }
              : { icon: 'x', color: 'red', onClick: this.clear })}
            onKeyUp={(e) => e.key === 'Enter' ? this.browse() : ''}
          />
        </Segment>
        {pending ? 
          <Loader 
            className='search-loader'
            active 
            inline='centered' 
            size='big'
          >
            Downloaded {Math.round(browseStatus.percentComplete || 0)}% of Response
          </Loader>
          : 
          <div>
            {browseError ? 
              <span className='browse-error'>Failed to browse {username}</span> :
              <div className='browse-container'>
                {emptyTree ? 
                  <PlaceholderSegment icon='folder open' caption='No user share to display'/> : 
                  <Card className='browse-tree-card' raised>
                    <Card.Content>
                      <Card.Header>
                        <Icon name='circle' color='green'/>
                        {username}
                      </Card.Header>
                      <Card.Meta className='browse-meta'>
                        <span>
                          {`${info.files + info.lockedFiles} files in ${info.directories + info.lockedDirectories} directories (including ${info.lockedFiles} files in ${info.lockedDirectories} locked directories)`} {/* eslint-disable-line max-len */}
                        </span>
                      </Card.Meta>
                      <Segment className='browse-folderlist'>
                        <DirectoryTree 
                          tree={tree} 
                          selectedDirectoryName={name}
                          onSelect={(_, value) => this.selectDirectory(value)}
                        />
                      </Segment>
                    </Card.Content>
                  </Card>}
                {name && <Directory
                  marginTop={-20}
                  name={name}
                  locked={locked}
                  files={files}
                  username={username}
                  onClose={this.deselectDirectory}
                />}
              </div>
            }
          </div>}
      </div>
    );
  };
}

export default Browse;
