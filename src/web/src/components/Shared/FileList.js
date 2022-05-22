import React from 'react';

import { formatSeconds, formatBytes, getFileName, formatAttributes } from '../../lib/util';

import { 
  Header, 
  Table, 
  Icon, 
  List, 
  Checkbox,
} from 'semantic-ui-react';

const FileList = ({ directoryName, files, locked, onSelectionChange, disabled, onClose }) => (
  <div style={{opacity: locked ? 0.5 : 1}}>
    <Header 
      size='small' 
      className='filelist-header'
    >
      <div>
        <Icon size='large' name={locked ? 'lock' : 'folder'}/>
        {directoryName}
     
        {!!onClose && <Icon 
          className='close-button' 
          name='close' 
          color='red'
          link
          onClick={() => onClose()}
        />}
      </div>
    </Header>
    {files && files.length > 0 && <List>
      <List.Item>
        <Table>
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell className='filelist-selector'>
                <Checkbox 
                  fitted
                  onChange={(event, data) => files.map(f => onSelectionChange(f, data.checked))}
                  checked={files.filter(f => !f.selected).length === 0}
                  disabled={disabled}
                />
              </Table.HeaderCell>
              <Table.HeaderCell className='filelist-filename'>File</Table.HeaderCell>
              <Table.HeaderCell className='filelist-size'>Size</Table.HeaderCell>
              <Table.HeaderCell className='filelist-attributes'>Attributes</Table.HeaderCell>
              <Table.HeaderCell className='filelist-length'>Length</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {files.sort((a, b) => a.filename > b.filename ? 1 : -1).map((f, i) => 
              <Table.Row key={i}>
                <Table.Cell className='filelist-selector'>
                  <Checkbox 
                    fitted 
                    onChange={(event, data) => onSelectionChange(f, data.checked)}
                    checked={f.selected}
                    disabled={disabled}
                  />
                </Table.Cell>
                <Table.Cell className='filelist-filename'>
                  {locked ? <Icon name='lock' /> : ''}{getFileName(f.filename)}
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
  </div>
);

export default FileList;
