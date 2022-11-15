import React, { Component } from 'react';
import * as transfers from '../../lib/transfers';

import { formatBytes, getDirectoryName } from '../../lib/util';

import FileList from '../Shared/FileList';

import { 
  Button, 
  Card, 
  Icon,
  Label,
} from 'semantic-ui-react';
import { getDirectoryContents } from '../../lib/users';

const buildTree = (response) => {
  let { files = [], lockedFiles = [] } = response;
  files = files.concat(lockedFiles.map(file => ({ ...file, locked: true })));

  return files.reduce((dict, file) => {
    let dir = getDirectoryName(file.filename);
    let selectable = { selected: false, ...file };
    dict[dir] = dict[dir] === undefined ? [ selectable ] : dict[dir].concat(selectable);
    return dict;
  }, {});
};

class Response extends Component {
  state = { 
    tree: buildTree(this.props.response), 
    downloadRequest: undefined, 
    downloadError: '',
    isFolded: this.props.isInitiallyFolded,
  };

  componentDidUpdate = (prevProps) => {
    if (JSON.stringify(this.props.response) !== JSON.stringify(prevProps.response)) {
      this.setState({ tree: buildTree(this.props.response) });
    }
    if (this.props.isInitiallyFolded !== prevProps.isInitiallyFolded) {
      this.setState({ isFolded: this.props.isInitiallyFolded });
    }
  };

  onFileSelectionChange = (file, state) => {
    file.selected = state;
    this.setState({ tree: this.state.tree, downloadRequest: undefined, downloadError: '' });
  };

  download = (username, files) => {
    this.setState({ downloadRequest: 'inProgress' }, async () => {
      try {
        const requests = (files || []).map(({ filename, size }) => ({ filename, size }));
        await transfers.download({ username, files: requests });

        this.setState({ downloadRequest: 'complete' });
      } catch (err) {
        this.setState({ downloadRequest: 'error', downloadError: err.response });
      }
    });
  };

  getFullDirectory = async (username, directory) => {
    const { name, files } = await getDirectoryContents({ username, directory });
    files.forEach((file, index) => {
      const newFilename = `${directory}\\${file.filename}`;
      files[index] = { ...files[index], filename: newFilename };
    });

    const newTree = this.state.tree;
    newTree[name] = files;
    this.setState({ tree: { ...newTree } });
  };

  toggleFolded = () => {
    this.setState({ isFolded: !this.state.isFolded });
  };

  render = () => {
    let {response} = this.props;
    let free = response.hasFreeUploadSlot;

    let { tree, downloadRequest, downloadError, isFolded } = this.state;

    let selectedFiles = Object.keys(tree)
      .reduce((list, dict) => list.concat(tree[dict]), [])
      .filter(f => f.selected);

    let selectedSize = formatBytes(selectedFiles.reduce((total, f) => total + f.size, 0));

    return (
      <Card className='result-card' raised>
        <Card.Content>
          <Card.Header>
            <Icon
              link
              name={isFolded ? 'chevron right' : 'chevron down'}
              onClick={this.toggleFolded}
            />
            <Icon name='circle' color={free ? 'green' : 'yellow'}/>
            {response.username}
            <Icon 
              className='close-button' 
              name='close' 
              color='red' 
              link
              onClick={() => this.props.onHide()}
            />
          </Card.Header>
          <Card.Meta className='result-meta'>
            <span>
              Upload Speed: {formatBytes(response.uploadSpeed)}/s,
              Free Upload Slot: {free ? 'YES' : 'NO'}, Queue Length: {response.queueLength}</span>
          </Card.Meta>
          {((!isFolded && Object.keys(tree)) || []).map((dir, i) => 
            <FileList 
              key={i}
              directoryName={dir}
              locked={tree[dir].find(file => file.locked)}
              files={tree[dir]}
              disabled={downloadRequest === 'inProgress'}
              onSelectionChange={this.onFileSelectionChange}
              footer={<button
                onClick={() => this.getFullDirectory(response.username, dir)} 
                style={{ cursor: 'pointer', width: '100%', backgroundColor: 'transparent', border: 'none' }}>
                <Icon name='folder'/>Get Full Directory Contents
              </button>}
            />
          )}
        </Card.Content>
        {selectedFiles.length > 0 && <Card.Content extra>
          <span>
            <Button 
              color='green' 
              content='Download'
              icon='download' 
              label={{ 
                as: 'a', 
                basic: false, 
                content: `${selectedFiles.length} file${selectedFiles.length === 1 ? '' : 's'}, ${selectedSize}`,
              }}
              labelPosition='right'
              onClick={() => this.download(response.username, selectedFiles)}
              disabled={this.props.disabled || downloadRequest === 'inProgress'}
            />
            {downloadRequest === 'inProgress' && <Icon loading name='circle notch' size='large'/>}
            {downloadRequest === 'complete' && <Icon name='checkmark' color='green' size='large'/>}
            {downloadRequest === 'error' && <span>
              <Icon name='x' color='red' size='large'/>
              <Label>{downloadError.data + ` (HTTP ${downloadError.status} ${downloadError.statusText})`}</Label>
            </span>}
          </span>
        </Card.Content>}
      </Card>
    );
  };
}

export default Response;