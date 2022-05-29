import React from 'react';

import {
  Table,
  Icon,
} from 'semantic-ui-react';

import { Link } from 'react-router-dom';

const ShareTable = ({ shares, onClick }) => {
  return (
    <Table>
      <Table.Header>
        <Table.Row>
          <Table.HeaderCell>Local Path</Table.HeaderCell>
          <Table.HeaderCell className='share-count-column'>Directories</Table.HeaderCell>
          <Table.HeaderCell className='share-count-column'>Files</Table.HeaderCell>
          <Table.HeaderCell>Alias</Table.HeaderCell>
          <Table.HeaderCell>Remote Path</Table.HeaderCell>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        {shares.map((share, index) => (<Table.Row key={index}>
          <Table.Cell onClick={() => onClick(share)}>
            <Icon name='folder'/>  
            <Link to='#'>{share.localPath}</Link>
          </Table.Cell>
          <Table.Cell>{share.directories}</Table.Cell>
          <Table.Cell>{share.files}</Table.Cell>
          <Table.Cell>{share.alias}</Table.Cell>
          <Table.Cell>{share.remotePath}</Table.Cell>
        </Table.Row>))}
      </Table.Body>
    </Table>
  );
};

export default ShareTable;