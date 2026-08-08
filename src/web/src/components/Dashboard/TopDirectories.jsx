import React from 'react';
import { Header, Icon, Table } from 'semantic-ui-react';

const getLastTwoSegments = (path) => {
  if (!path) {
    return path;
  }

  const parts = path.split(/[/\\]/u).filter((p) => p.length > 0);

  if (parts.length <= 2) {
    return parts.join('/');
  }

  return parts.slice(-2).join('/');
};

const TopDirectories = ({ rows }) => (
  <>
    <Header size="small">
      <Icon name="folder open" /> Directories
    </Header>
    <Table
      className="unstackable"
      compact="very"
    >
      <Table.Header>
        <Table.Row>
          <Table.HeaderCell
            style={{ color: '#999', width: '2em' }}
            textAlign="right"
          >
            #
          </Table.HeaderCell>
          <Table.HeaderCell>Directory</Table.HeaderCell>
          <Table.HeaderCell textAlign="right">Downloads</Table.HeaderCell>
          <Table.HeaderCell textAlign="right">Distinct Users</Table.HeaderCell>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        {(!rows || rows.length === 0) && (
          <Table.Row>
            <Table.Cell
              colSpan={4}
              style={{ opacity: 0.5, textAlign: 'center' }}
            >
              No data to display
            </Table.Cell>
          </Table.Row>
        )}
        {rows &&
          rows.map((row, index) => (
            <Table.Row key={row.directory}>
              <Table.Cell
                style={{ color: '#999' }}
                textAlign="right"
              >
                {index + 1}
              </Table.Cell>
              <Table.Cell title={row.directory}>
                {getLastTwoSegments(row.directory)}
              </Table.Cell>
              <Table.Cell textAlign="right">
                {row.count.toLocaleString()}
              </Table.Cell>
              <Table.Cell textAlign="right">
                {row.distinctUsers.toLocaleString()}
              </Table.Cell>
            </Table.Row>
          ))}
      </Table.Body>
    </Table>
  </>
);

export default TopDirectories;
