import React from 'react';

import {
  Table,
  Icon,
} from 'semantic-ui-react';

import { Switch } from '../../Shared';

const ExclusionTable = ({ exclusions = [] } = {}) => {
  return (
    <Table>
      <Table.Header>
        <Table.Row>
          <Table.HeaderCell>Excluded Paths</Table.HeaderCell>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        <Switch
          empty={exclusions.length === 0 && <Table.Row>
            <Table.Cell style={{ opacity: .5, padding: '10px !important', textAlign: 'center' }}>
              No exclusions configured
            </Table.Cell>
          </Table.Row>}
        >
          {exclusions.map((share, index) => (<Table.Row key={index}>
            <Table.Cell><Icon name='x' color='red'/>{share.localPath}</Table.Cell>
          </Table.Row>))}
        </Switch>
      </Table.Body>
    </Table>
  );
};

export default ExclusionTable;