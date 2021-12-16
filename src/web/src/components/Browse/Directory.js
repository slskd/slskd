import React, { Component } from 'react';
import * as transfers from '../../lib/transfers';

import { formatBytes } from '../../lib/util';

import FileList from '../Shared/FileList'

import { 
  Button, 
  Card, 
  Icon,
  Label
} from 'semantic-ui-react';

const initialState = {
  downloadRequest: undefined,
  downloadError: ''
}

class Directory extends Component {
  state = { 
    ...initialState,
    files: this.props.files.map(f => ({ selected: false, ...f }))
  }

  onFileSelectionChange = (file, state) => {
    file.selected = state;
    this.setState({ tree: this.state.tree, downloadRequest: undefined, downloadError: '' })
  }

  download = (username, files) => {
    this.setState({ downloadRequest: 'inProgress' }, async () => {
      try {
          const requests = (files || []).map(({ filename, size }) => ({ filename, size }))
          await transfers.download({ username, files: requests })

          this.setState({ downloadRequest: 'complete' })
      } catch (err) {
          this.setState({ downloadRequest: 'error', downloadError: err.response })
      }
  });
  }

  componentDidUpdate = (prevProps) => {
    if (this.props.name !== prevProps.name) {
      this.setState({ files: this.props.files.map(f => ({ selected: false, ...f }))});
    }
  }

  render = () => {
    let { username, name, locked, marginTop, onClose } = this.props;
    let { files, downloadRequest, downloadError } = this.state;

    let selectedFiles = files
      .filter(f => f.selected);

    let selectedSize = formatBytes(selectedFiles.reduce((total, f) => total + f.size, 0));

    return (
      <Card className='result-card' raised>
        <Card.Content>
          <div style={{marginTop: marginTop || 0}}>
            <FileList 
              directoryName={name}
              locked={locked}
              files={files}
              disabled={downloadRequest === 'inProgress'}
              onSelectionChange={this.onFileSelectionChange}
              onClose={onClose}
            />
          </div>
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
                content: `${selectedFiles.length} file${selectedFiles.length === 1 ? '' : 's'}, ${selectedSize}`
              }}
              labelPosition='right'
              onClick={() => this.download(username, selectedFiles)}
              disabled={downloadRequest === 'inProgress'}
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
    )
  }
}

export default Directory;
