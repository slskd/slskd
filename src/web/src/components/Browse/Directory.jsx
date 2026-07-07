import * as transfers from '../../lib/transfers';
import { formatBytes } from '../../lib/util';
import FileList from '../Shared/FileList';
import React, { Component } from 'react';
import { Button, Card, Icon, Label } from 'semantic-ui-react';

const initialState = {
  downloadError: '',
  downloadRequest: undefined,
};

class Directory extends Component {
  constructor(props) {
    super(props);

    this.state = {
      ...initialState,
      files: this.props.files.map((f) => ({ selected: false, ...f })),
    };
  }

  componentDidUpdate(previousProps) {
    if (this.props.name !== previousProps.name) {
      this.setState({
        files: this.props.files.map((f) => ({ selected: false, ...f })),
      });
    }
  }

  handleFileSelectionChange = (file, state) => {
    file.selected = state;
    this.setState((previousState) => ({
      downloadError: '',
      downloadRequest: undefined,
      tree: previousState.tree,
    }));
  };

  download = (username, files) => {
    this.setState({ downloadRequest: 'inProgress' }, async () => {
      try {
        const requests = (files || []).map(({ filename, size }) => ({
          filename,
          size,
        }));
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

  render() {
    const {
      allFilesInSubtree,
      hasSubfolders,
      locked,
      marginTop,
      name,
      onClose,
      username,
    } = this.props;
    const { downloadError, downloadRequest, files } = this.state;

    const selectedFiles = files.filter((f) => f.selected);

    const selectedSize = formatBytes(
      selectedFiles.reduce((total, f) => total + f.size, 0),
    );

    const subtreeSize = formatBytes(
      (allFilesInSubtree || []).reduce((total, f) => total + f.size, 0),
    );

    return (
      <Card
        className="result-card"
        raised
      >
        <Card.Content>
          <div style={{ marginTop: marginTop || 0 }}>
            <FileList
              directoryName={name}
              disabled={downloadRequest === 'inProgress'}
              files={files}
              locked={locked}
              onClose={onClose}
              onSelectionChange={this.handleFileSelectionChange}
            />
          </div>
        </Card.Content>
        {(selectedFiles.length > 0 || hasSubfolders) && (
          <Card.Content extra>
            <span>
              {selectedFiles.length > 0 && (
                <Button
                  color="green"
                  content="Download"
                  disabled={downloadRequest === 'inProgress'}
                  icon="download"
                  label={{
                    as: 'a',
                    basic: false,
                    content: `${selectedFiles.length} file${selectedFiles.length === 1 ? '' : 's'}, ${selectedSize}`,
                  }}
                  labelPosition="right"
                  onClick={() => this.download(username, selectedFiles)}
                />
              )}
              {hasSubfolders && (
                <Button
                  color="blue"
                  content="Download Folder (+ Subfolders)"
                  disabled={downloadRequest === 'inProgress'}
                  icon="folder open"
                  label={{
                    as: 'a',
                    basic: false,
                    content: `${allFilesInSubtree.length} file${allFilesInSubtree.length === 1 ? '' : 's'}, ${subtreeSize}`,
                  }}
                  labelPosition="right"
                  onClick={() => this.download(username, allFilesInSubtree)}
                  style={{ marginLeft: selectedFiles.length > 0 ? '8px' : 0 }}
                />
              )}
              {downloadRequest === 'inProgress' && (
                <Icon
                  loading
                  name="circle notch"
                  size="large"
                />
              )}
              {downloadRequest === 'complete' && (
                <Icon
                  color="green"
                  name="checkmark"
                  size="large"
                />
              )}
              {downloadRequest === 'error' && (
                <span>
                  <Icon
                    color="red"
                    name="x"
                    size="large"
                  />
                  <Label>
                    {downloadError.data +
                      ` (HTTP ${downloadError.status} ${downloadError.statusText})`}
                  </Label>
                </span>
              )}
            </span>
          </Card.Content>
        )}
      </Card>
    );
  }
}

export default Directory;
