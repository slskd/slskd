import { formatDate, getFileName, truncate } from '../../lib/util';
import React from 'react';
import {
  Button,
  ButtonGroup,
  Header,
  Icon,
  Loader,
  Table,
} from 'semantic-ui-react';

const DIRECTIONS = ['Upload', 'Download', 'All'];

const ExceptionList = ({ direction, loading, onDirectionChange, rows }) => (
  <>
    <Header
      size="small"
      style={{ alignItems: 'center', display: 'flex' }}
    >
      <Icon name="clipboard outline" />
      <Header.Content>Recent Errors</Header.Content>
      <ButtonGroup
        size="mini"
        style={{ marginLeft: 'auto' }}
      >
        {DIRECTIONS.map((d) => (
          <Button
            active={direction === d}
            key={d}
            onClick={() => onDirectionChange(d)}
          >
            {d}
          </Button>
        ))}
      </ButtonGroup>
    </Header>
    <Table
      className="unstackable"
      compact="very"
    >
      <Table.Header>
        <Table.Row>
          <Table.HeaderCell>Time</Table.HeaderCell>
          <Table.HeaderCell>Direction</Table.HeaderCell>
          <Table.HeaderCell>Username</Table.HeaderCell>
          <Table.HeaderCell>Filename</Table.HeaderCell>
          <Table.HeaderCell>Exception</Table.HeaderCell>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        {loading && (
          <Table.Row>
            <Table.Cell
              colSpan={5}
              textAlign="center"
            >
              <Loader
                active
                inline="centered"
                size="small"
              />
            </Table.Cell>
          </Table.Row>
        )}
        {rows.length === 0 && !loading && (
          <Table.Row>
            <Table.Cell
              colSpan={5}
              style={{ opacity: 0.5, textAlign: 'center' }}
            >
              No data to display
            </Table.Cell>
          </Table.Row>
        )}
        {!loading &&
          rows.map((row) => (
            <Table.Row key={`${row.direction}-${row.endedAt}-${row.filename}`}>
              <Table.Cell style={{ whiteSpace: 'nowrap' }}>
                {row.endedAt ? formatDate(row.endedAt) : ''}
              </Table.Cell>
              <Table.Cell>{row.direction}</Table.Cell>
              <Table.Cell>{row.username}</Table.Cell>
              <Table.Cell title={row.filename}>
                {row.filename ? getFileName(row.filename) : ''}
              </Table.Cell>
              <Table.Cell title={row.exception}>
                {truncate(row.exception, 80)}
              </Table.Cell>
            </Table.Row>
          ))}
      </Table.Body>
    </Table>
  </>
);

export default ExceptionList;
