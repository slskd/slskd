import '../System.css';
import React from 'react';
import { Icon, Table } from 'semantic-ui-react';

const abbreviations = {
  Debug: 'DBG',
  Error: 'ERR',
  Fatal: 'FTL',
  Information: 'INF',
  Verbose: 'VRB',
  Warning: 'WRN',
};

export const formatTimestamp = (timestamp) => {
  const date = new Date(timestamp);
  return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`; // eslint-disable-line max-len
};

/**
 * Renders log records.  Shared by the Live and History views; both consume the
 * same record shape ({ timestamp, level, message }), the former from the logs
 * hub and the latter from the logs API.
 *
 * Passing onSortChange makes the Timestamp column sortable; omitting it (as the
 * Live view does) renders a static header.
 */
const LogTable = ({
  emptyMessage = 'No logs',
  logs,
  onSortChange,
  sortDirection,
}) => (
  <Table
    className="logs-table"
    compact="very"
  >
    <Table.Header>
      <Table.Row>
        <Table.HeaderCell
          onClick={onSortChange ? () => onSortChange() : undefined}
          style={onSortChange ? { cursor: 'pointer' } : undefined}
        >
          Timestamp
          {Boolean(onSortChange) && (
            <Icon
              name={
                sortDirection === 'ascending' ? 'chevron up' : 'chevron down'
              }
            />
          )}
        </Table.HeaderCell>
        <Table.HeaderCell>Level</Table.HeaderCell>
        <Table.HeaderCell>Message</Table.HeaderCell>
      </Table.Row>
    </Table.Header>
    <Table.Body className="logs-table-body">
      {logs?.length === 0 ? (
        <Table.Row>
          <Table.Cell
            colSpan={99}
            style={{
              opacity: 0.5,
              padding: '10px !important',
              textAlign: 'center',
            }}
          >
            {emptyMessage}
          </Table.Cell>
        </Table.Row>
      ) : (
        logs.map((log) => (
          <Table.Row
            disabled={log.level === 'Debug' || log.level === 'Verbose'}
            key={log.id}
            negative={log.level === 'Error' || log.level === 'Fatal'}
            warning={log.level === 'Warning'}
          >
            <Table.Cell>{formatTimestamp(log.timestamp)}</Table.Cell>
            <Table.Cell>{abbreviations[log.level] || log.level}</Table.Cell>
            <Table.Cell className="logs-table-message">
              {log.message}
            </Table.Cell>
          </Table.Row>
        ))
      )}
    </Table.Body>
  </Table>
);

export default LogTable;
