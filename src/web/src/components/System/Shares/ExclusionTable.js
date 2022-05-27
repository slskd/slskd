import React from 'react';

import {
  Table,
  Icon,
} from 'semantic-ui-react';

const ExclusionTable = ({ exclusions = [] } = {}) => {
  if (exclusions.length === 0) {
    return <></>;
  }

  return (
    <Table>
      <Table.Header>
        <Table.Row>
          <Table.HeaderCell>Excluded Paths</Table.HeaderCell>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        {exclusions.map((share, index) => (<Table.Row key={index}>
          <Table.Cell><Icon name='x' color='red'/>{share.localPath}</Table.Cell>
        </Table.Row>))}
      </Table.Body>
    </Table>
  );
};

export default ExclusionTable;