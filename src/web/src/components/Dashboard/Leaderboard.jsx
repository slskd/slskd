import * as reports from '../../lib/reports';
import { formatBytes, formatSpeed } from '../../lib/util';
import React, { useEffect, useRef, useState } from 'react';
import { Divider, Grid, Header, Icon, Loader, Table } from 'semantic-ui-react';

const SORT_FIELDS = [
  { label: 'Count', sort: 'Count' },
  { label: 'Total Size', sort: 'TotalBytes' },
  { label: 'Avg Speed', sort: 'AverageSpeed' },
];

const sortHeaderStyle = { cursor: 'pointer', userSelect: 'none' };

const LeaderboardTable = ({ loading, onSort, rows, sortBy }) => (
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
        <Table.HeaderCell>Username</Table.HeaderCell>
        {SORT_FIELDS.map(({ label, sort }) => (
          <Table.HeaderCell
            key={sort}
            onClick={() => onSort(sort)}
            style={sortHeaderStyle}
            textAlign="right"
          >
            {label}
            {sortBy === sort && (
              <Icon
                name="chevron down"
                style={{ marginLeft: '0.3em' }}
              />
            )}
          </Table.HeaderCell>
        ))}
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
      {!loading && (!rows || rows.length === 0) && (
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
        rows &&
        rows.map((row, index) => (
          <Table.Row key={row.username}>
            <Table.Cell
              style={{ color: '#999' }}
              textAlign="right"
            >
              {index + 1}
            </Table.Cell>
            <Table.Cell>{row.username}</Table.Cell>
            <Table.Cell textAlign="right">
              {row.count.toLocaleString()}
            </Table.Cell>
            <Table.Cell textAlign="right">
              {formatBytes(row.totalBytes)}
            </Table.Cell>
            <Table.Cell textAlign="right">
              {formatSpeed(row.averageSpeed)}
            </Table.Cell>
          </Table.Row>
        ))}
    </Table.Body>
  </Table>
);

const initialState = {
  sortBy: 'Count',
  rows: {
    upload: [],
    download: [],
  },
  loading: {
    upload: false,
    download: false,
  },
};

const Leaderboard = ({ downloads, end, start, uploads }) => {
  const [state, setState] = useState(initialState);
  const sortRef = useRef(null);

  useEffect(() => {
    setState((previous) => ({
      ...previous,
      sortBy: 'Count',
      rows: { upload: uploads, download: downloads },
    }));
  }, [downloads, uploads]);

  const onSort = async (sort) => {
    if (sort === state.sortBy) return;

    sortRef.current = sort;

    setState((previous) => ({
      ...previous,
      sortBy: sort,
      loading: { upload: true, download: true },
    }));

    const startDate = start ? new Date(start) : undefined;
    const endDate = end ? new Date(end) : new Date();

    let newUploads = state.rows.upload;
    let newDownloads = state.rows.download;

    try {
      [newUploads, newDownloads] = await Promise.all([
        reports.getLeaderboard({
          direction: 'Upload',
          end: endDate,
          sortBy: sort,
          start: startDate,
        }),
        reports.getLeaderboard({
          direction: 'Download',
          end: endDate,
          sortBy: sort,
          start: startDate,
        }),
      ]);
    } catch (error) {
      console.error(error);
    }

    if (sortRef.current !== sort) return;

    setState((previous) => ({
      ...previous,
      rows: { upload: newUploads, download: newDownloads },
      loading: { upload: false, download: false },
    }));
  };

  return (
    <Grid
      columns={2}
      stackable
    >
      <Grid.Column>
        <Header size="small">
          <Icon name="download" /> Downloads
        </Header>
        <LeaderboardTable
          loading={state.loading.download}
          onSort={onSort}
          rows={state.rows.download}
          sortBy={state.sortBy}
        />
      </Grid.Column>
      <Divider vertical />
      <Grid.Column>
        <Header size="small">
          <Icon name="upload" /> Uploads
        </Header>
        <LeaderboardTable
          loading={state.loading.upload}
          onSort={onSort}
          rows={state.rows.upload}
          sortBy={state.sortBy}
        />
      </Grid.Column>
    </Grid>
  );
};

export default Leaderboard;
