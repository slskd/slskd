import React from 'react';

import { formatSeconds, formatBytes, getFileName, formatAttributes } from '../../lib/util';

import { 
  Header, 
  Table, 
  Icon, 
  List, 
  Checkbox,
} from 'semantic-ui-react';

class FileList extends React.Component {
  state = {
    isFolded: false,
  };

  toggleFolded = () => {
    if (!this.props.locked) {
      this.setState({'isFolded': !this.state.isFolded});
    }
  };

  render() {
    return (
      <div style={{opacity: this.props.locked ? 0.5 : 1}}>
        <Header 
          size='small' 
          className='filelist-header'
        >
          <div>
            <Icon size='large' 
              link={!this.props.locked}
              name={this.props.locked ? 'lock' : this.state.isFolded ? 'folder' : 'folder open'} 
              onClick={() => this.toggleFolded()}/>
            {this.props.directoryName}
        
            {!!this.props.onClose && <Icon 
              className='close-button' 
              name='close' 
              color='red'
              link
              onClick={() => this.props.onClose()}
            />}
          </div>
        </Header>
        {!this.state.isFolded && this.props.files && this.props.files.length > 0 && <List>
          <List.Item>
            <Table>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell className='filelist-selector'>
                    <Checkbox 
                      fitted
                      onChange={(event, data) => this.props.files.map(f => 
                        this.props.onSelectionChange(f, data.checked))}
                      checked={this.props.files.filter(f => !f.selected).length === 0}
                      disabled={this.props.disabled}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell className='filelist-filename'>File</Table.HeaderCell>
                  <Table.HeaderCell className='filelist-size'>Size</Table.HeaderCell>
                  <Table.HeaderCell className='filelist-attributes'>Attributes</Table.HeaderCell>
                  <Table.HeaderCell className='filelist-length'>Length</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {this.props.files.sort((a, b) => a.filename > b.filename ? 1 : -1).map((f, i) => 
                  <Table.Row key={i}>
                    <Table.Cell className='filelist-selector'>
                      <Checkbox 
                        fitted 
                        onChange={(event, data) => this.props.onSelectionChange(f, data.checked)}
                        checked={f.selected}
                        disabled={this.props.disabled}
                      />
                    </Table.Cell>
                    <Table.Cell className='filelist-filename'>
                      {this.props.locked ? <Icon name='lock' /> : ''}{getFileName(f.filename)}
                    </Table.Cell>
                    <Table.Cell className='filelist-size'>{formatBytes(f.size)}</Table.Cell>
                    <Table.Cell className='filelist-attributes'>{formatAttributes(f)}</Table.Cell>
                    <Table.Cell className='filelist-length'>{formatSeconds(f.length)}</Table.Cell>
                  </Table.Row>
                )}
              </Table.Body>
            </Table>
          </List.Item>
        </List>}
      </div>);
  }
}

export default FileList;
