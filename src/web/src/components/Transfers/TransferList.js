import React, { Component } from 'react';

import {
  Checkbox,
} from 'semantic-ui-react';

import { formatBytes, getFileName } from '../../lib/util';

import { 
  Header, 
  Table, 
  Icon, 
  List, 
  Progress,
  Button,
} from 'semantic-ui-react';

const getColor = (state) => {
  switch(state) {
  case 'InProgress':
    return { color: 'blue' }; 
  case 'Completed, Succeeded':
    return { color: 'green' };
  case 'Requested':
  case 'Queued, Locally':
  case 'Queued, Remotely':
  case 'Queued':
    return {};
  case 'Initializing':
    return { color: 'teal' };
  default:
    return { color: 'red' };
  }
};

const isRetryableState = (state) => getColor(state).color === 'red';
const isQueuedState = (state) => state.includes('Queued');

const formatBytesTransferred = ({ transferred, size }) => {
  const [t, tExt] = formatBytes(transferred).split(' ');
  const [s, sExt] = formatBytes(size).split(' ');

  const fmt = (n) => parseFloat(n).toFixed(2);

  // if less than 1 MB has been transferred, don't include decimals
  if (tExt === 'KB') {
    return `${t} KB/${fmt(s)} ${sExt}`;
  }

  // if the suffix for size and transferred doesn't match, include
  // the suffix for each
  if (tExt !== sExt) {
    return `${fmt(t)} ${tExt}/${fmt(s)} ${sExt}`;
  }

  return `${fmt(t)}/${fmt(s)} ${sExt}`;
};

class TransferList extends Component {
  handleClick = (file) => {
    const { state, direction } = file;

    if (direction === 'Download') {
      if (isRetryableState(state)) {
        return this.props.onRetryRequested(file);
      }
    
      if (isQueuedState(state)) {
        return this.props.onPlaceInQueueRequested(file);
      }
    }    
  };

  render = () => {
    const { directoryName, onSelectionChange, files } = this.props;

    return (
      <div>
        <Header 
          size='small' 
          className='filelist-header'
        >
          <Icon name='folder'/>{directoryName}
        </Header>
        <List>
          <List.Item>
            <Table>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell className='transferlist-selector'>
                    <Checkbox 
                      fitted 
                      checked={files.filter(f => !f.selected).length === 0}
                      onChange={(event, data) =>
                        files.map(file => onSelectionChange(directoryName, file, data.checked))}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell className='transferlist-filename'>File</Table.HeaderCell>
                  <Table.HeaderCell className='transferlist-progress'>Progress</Table.HeaderCell>
                  <Table.HeaderCell className='transferlist-size'>Size</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {files.sort((a, b) => getFileName(a.filename).localeCompare(getFileName(b.filename))).map((f, i) => 
                  <Table.Row key={i}>
                    <Table.Cell className='transferlist-selector'>
                      <Checkbox 
                        fitted 
                        checked={f.selected}
                        onChange={(event, data) => onSelectionChange(directoryName, f, data.checked)}
                      />
                    </Table.Cell>
                    <Table.Cell className='transferlist-filename'>{getFileName(f.filename)}</Table.Cell>
                    <Table.Cell className='transferlist-progress'>
                      {f.state === 'InProgress' ? 
                        <Progress 
                          style={{ margin: 0 }}
                          percent={Math.round(f.percentComplete)} 
                          progress color={getColor(f.state).color}
                        /> : 
                        <Button 
                          fluid 
                          size='mini' 
                          style={{ margin: 0, padding: 7, cursor: f.direction === 'Upload' ? 'unset' : '' }} 
                          {...getColor(f.state)} 
                          onClick={() => this.handleClick(f)}
                          active={f.direction === 'Upload'}
                        >
                          {f.direction === 'Download' && isQueuedState(f.state) && <Icon name='refresh'/>}
                          {f.direction === 'Download' && isRetryableState(f.state) && <Icon name='redo'/>}
                          {f.state}{f.placeInQueue ? ` (#${f.placeInQueue})` : ''}
                        </Button>}
                    </Table.Cell>
                    <Table.Cell className='transferlist-size'>
                      {f.bytesTransferred > 0
                        ? formatBytesTransferred({ transferred: f.bytesTransferred, size: f.size })
                        : ''}
                    </Table.Cell>
                  </Table.Row>
                )}
              </Table.Body>
            </Table>
          </List.Item>
        </List>
      </div>
    );
  };
}

export default TransferList;
