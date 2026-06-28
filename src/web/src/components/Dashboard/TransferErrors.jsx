import { Graph, useDarkMode } from '../Shared';
import React, { useMemo } from 'react';
import { Divider, Header, Table } from 'semantic-ui-react';

const truncate = (text, maxLength) => {
  if (!text) {
    return '';
  }

  if (text.length <= maxLength) {
    return text;
  }

  return `${text.slice(0, maxLength)}…`;
};

const getFilename = (path) => {
  if (!path) {
    return '';
  }

  const parts = path.replaceAll('\\', '/').split('/');
  return parts[parts.length - 1] || path;
};

const formatDateTime = (isoString) => {
  if (!isoString) {
    return '';
  }

  return new Date(isoString).toLocaleString();
};

const mergePareto = (uploadRows, downloadRows) => {
  const combined = {};
  for (const row of [...(uploadRows || []), ...(downloadRows || [])]) {
    const key = row.exception ?? '';
    combined[key] = combined[key]
      ? {
          exception: key,
          count: combined[key].count + row.count,
          distinctUsers: combined[key].distinctUsers + row.distinctUsers,
        }
      : { ...row };
  }

  return Object.values(combined)
    .sort((a, b) => b.count - a.count)
    .slice(0, 10);
};

const mergeRecent = (uploadRows, downloadRows) =>
  [
    ...(uploadRows || []).map((r) => ({ ...r, direction: 'Upload' })),
    ...(downloadRows || []).map((r) => ({ ...r, direction: 'Download' })),
  ]
    .sort((a, b) => new Date(b.endedAt) - new Date(a.endedAt))
    .slice(0, 10);

const ERRORS_SERIES = [
  {
    color: '#db2828',
    format: (v) => v.toLocaleString(),
    key: 'uploadErrors',
    name: 'Upload Errors',
    unit: 'count',
  },
  {
    color: '#a333c8',
    format: (v) => v.toLocaleString(),
    key: 'downloadErrors',
    name: 'Download Errors',
    unit: 'count',
  },
];

const DEFAULT_ERRORS_SERIES = new Set(['uploadErrors', 'downloadErrors']);

const TransferErrors = ({ chartData, download, upload }) => {
  const isDark = useDarkMode();

  const paretoRows = useMemo(
    () => mergePareto(upload.pareto, download.pareto),
    [upload.pareto, download.pareto],
  );
  const recentRows = useMemo(
    () => mergeRecent(upload.recent, download.recent),
    [upload.recent, download.recent],
  );

  const maxParetoCount =
    paretoRows && paretoRows.length > 0 ? paretoRows[0].count : 1;

  return (
    <div>
      <Header as="h5">Error Count Over Time</Header>
      <Graph
        data={chartData ?? []}
        defaultSeries={DEFAULT_ERRORS_SERIES}
        series={ERRORS_SERIES}
      />
      <Divider />
      <Header as="h5">
        Top Fault Types
        <span
          style={{
            color: '#999',
            float: 'right',
            fontSize: '0.8em',
            fontWeight: 'normal',
          }}
        >
          pareto
        </span>
      </Header>
      <Table
        className="unstackable"
        compact="very"
      >
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell>Exception</Table.HeaderCell>
            <Table.HeaderCell style={{ width: '120px' }} />
            <Table.HeaderCell textAlign="right">Count</Table.HeaderCell>
            <Table.HeaderCell textAlign="right">
              Distinct Users
            </Table.HeaderCell>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {(!paretoRows || paretoRows.length === 0) && (
            <Table.Row>
              <Table.Cell
                colSpan={4}
                style={{ opacity: 0.5, textAlign: 'center' }}
              >
                No data to display.
              </Table.Cell>
            </Table.Row>
          )}
          {paretoRows &&
            paretoRows.map((row) => (
              <Table.Row key={row.exception ?? ''}>
                <Table.Cell title={row.exception}>
                  {truncate(row.exception, 80)}
                </Table.Cell>
                <Table.Cell>
                  <div
                    style={{
                      background: isDark ? 'rgba(255,255,255,0.08)' : '#f0f0f0',
                      borderRadius: '2px',
                      height: '7px',
                    }}
                  >
                    <div
                      style={{
                        background: '#db2828',
                        borderRadius: '2px',
                        height: '7px',
                        width: `${Math.round((row.count / maxParetoCount) * 100)}%`,
                      }}
                    />
                  </div>
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
      <Divider />
      <Header as="h5">Recent Errors</Header>
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
          {(!recentRows || recentRows.length === 0) && (
            <Table.Row>
              <Table.Cell
                colSpan={5}
                style={{ opacity: 0.5, textAlign: 'center' }}
              >
                No data to display.
              </Table.Cell>
            </Table.Row>
          )}
          {recentRows &&
            recentRows.map((row) => (
              <Table.Row
                key={`${row.direction}-${row.endedAt}-${row.filename}`}
              >
                <Table.Cell style={{ whiteSpace: 'nowrap' }}>
                  {formatDateTime(row.endedAt)}
                </Table.Cell>
                <Table.Cell>{row.direction}</Table.Cell>
                <Table.Cell>{row.username}</Table.Cell>
                <Table.Cell title={row.filename}>
                  {getFilename(row.filename)}
                </Table.Cell>
                <Table.Cell title={row.exception}>
                  {truncate(row.exception, 80)}
                </Table.Cell>
              </Table.Row>
            ))}
        </Table.Body>
      </Table>
    </div>
  );
};

export default TransferErrors;
