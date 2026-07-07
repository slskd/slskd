import { truncate } from '../../lib/util';
import React from 'react';
import {
  Button,
  ButtonGroup,
  Header,
  Icon,
  Loader,
  Progress,
  Table,
} from 'semantic-ui-react';

const DIRECTIONS = ['Upload', 'Download', 'All'];

const ExceptionPareto = ({ direction, loading, onDirectionChange, rows }) => {
  const maxCount = rows.length > 0 ? rows[0].count : 1;

  return (
    <>
      <Header
        size="small"
        style={{ alignItems: 'center', display: 'flex' }}
      >
        <Icon name="sort amount down" />
        <Header.Content>Error Count By Type</Header.Content>
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
            <Table.HeaderCell>Direction</Table.HeaderCell>
            <Table.HeaderCell>Exception</Table.HeaderCell>
            <Table.HeaderCell style={{ width: '120px' }} />
            <Table.HeaderCell textAlign="right">Count</Table.HeaderCell>
            <Table.HeaderCell textAlign="right">
              Distinct Users
            </Table.HeaderCell>
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
          {!loading && rows.length === 0 && (
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
              <Table.Row key={`${row.direction}-${row.exception ?? ''}`}>
                <Table.Cell>{row.direction}</Table.Cell>
                <Table.Cell title={row.exception}>
                  {truncate(row.exception, 80)}
                </Table.Cell>
                <Table.Cell>
                  <Progress
                    color="red"
                    percent={Math.round((row.count / maxCount) * 100)}
                    size="tiny"
                    style={{ margin: 0 }}
                  />
                </Table.Cell>
                <Table.Cell textAlign="right">
                  <strong>{row.count.toLocaleString()}</strong>
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
};

export default ExceptionPareto;
