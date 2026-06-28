import { Graph, LoaderSegment } from '../Shared';
import Leaderboard from './Leaderboard';
import TopDirectories from './TopDirectories';
import TransferErrors from './TransferErrors';
import React, { useMemo } from 'react';
import {
  Button,
  ButtonGroup,
  Divider,
  Header,
  Icon,
  Segment,
  Statistic,
  Tab,
} from 'semantic-ui-react';

const formatBytes = (bytes) => {
  if (!bytes || bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
  const index = Math.floor(Math.log(bytes) / Math.log(1_024));
  return `${(bytes / 1_024 ** index).toFixed(1)} ${units[index]}`;
};

const formatSpeed = (bytesPerSecond) => {
  if (!bytesPerSecond || bytesPerSecond === 0) return '0 B/s';
  return `${formatBytes(bytesPerSecond)}/s`;
};

const formatBytesParts = (bytes) => {
  if (!bytes || bytes === 0) return { unit: 'B', value: '0' };
  const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
  const index = Math.floor(Math.log(bytes) / Math.log(1_024));
  return { unit: units[index], value: (bytes / 1_024 ** index).toFixed(1) };
};

const sumCounts = (directionData = {}) =>
  Object.values(directionData).reduce((sum, s) => sum + (s.count ?? 0), 0);

const sumBytes = (directionData = {}) =>
  Object.values(directionData).reduce((sum, s) => sum + (s.totalBytes ?? 0), 0);

const errorCount = (directionData = {}) =>
  (directionData.Errored?.count ?? 0) +
  (directionData.Cancelled?.count ?? 0) +
  (directionData.TimedOut?.count ?? 0);

const formatWait = (seconds) => {
  if (!seconds || seconds === 0) return '0s';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  return `${(seconds / 60).toFixed(1)}m`;
};

const buildChartData = (histogram) =>
  Object.entries(histogram)
    .sort(([a], [b]) => new Date(a) - new Date(b))
    .map(([timestamp, directions]) => {
      const uploadBytes = sumBytes(directions.Upload ?? {});
      const downloadBytes = sumBytes(directions.Download ?? {});
      const uploadCount = sumCounts(directions.Upload ?? {});
      const downloadCount = sumCounts(directions.Download ?? {});
      const uploadErrors = errorCount(directions.Upload ?? {});
      const downloadErrors = errorCount(directions.Download ?? {});
      return {
        downloadBytes,
        downloadCount,
        downloadErrorRate:
          downloadCount > 0 ? (downloadErrors / downloadCount) * 100 : 0,
        downloadErrors,
        downloadSpeed: directions.Download?.Succeeded?.averageSpeed ?? 0,
        shareRatio: downloadBytes > 0 ? uploadBytes / downloadBytes : 0,
        timestamp: new Date(timestamp).getTime(),
        uploadBytes,
        uploadCount,
        uploadErrorRate:
          uploadCount > 0 ? (uploadErrors / uploadCount) * 100 : 0,
        uploadErrors,
        uploadSpeed: directions.Upload?.Succeeded?.averageSpeed ?? 0,
        uploadWait: directions.Upload?.Succeeded?.averageWait ?? 0,
      };
    });

const HISTORY_SERIES = [
  {
    color: '#21ba45',
    format: formatBytes,
    key: 'uploadBytes',
    name: 'Upload Size',
  },
  {
    color: '#2185d0',
    format: formatBytes,
    key: 'downloadBytes',
    name: 'Download Size',
  },
  {
    color: '#6435c9',
    format: (v) => v.toLocaleString(),
    key: 'uploadCount',
    name: 'Upload Count',
  },
  {
    color: '#e03997',
    format: (v) => v.toLocaleString(),
    key: 'downloadCount',
    name: 'Download Count',
  },
  {
    color: '#f2711c',
    format: formatSpeed,
    key: 'uploadSpeed',
    name: 'Upload Speed',
  },
  {
    color: '#fbbd08',
    format: formatSpeed,
    key: 'downloadSpeed',
    name: 'Download Speed',
  },
  {
    color: '#db2828',
    format: (v) => v.toLocaleString(),
    key: 'uploadErrors',
    name: 'Upload Errors',
  },
  {
    color: '#a333c8',
    format: (v) => v.toLocaleString(),
    key: 'downloadErrors',
    name: 'Download Errors',
  },
  {
    color: '#d4500a',
    format: (v) => `${v.toFixed(1)}%`,
    key: 'uploadErrorRate',
    name: 'Upload Error Rate',
  },
  {
    color: '#1aa9b0',
    format: (v) => `${v.toFixed(1)}%`,
    key: 'downloadErrorRate',
    name: 'Download Error Rate',
  },
  {
    color: '#8e44ad',
    format: formatWait,
    key: 'uploadWait',
    name: 'Upload Queue Wait',
  },
  {
    color: '#b5cc18',
    format: (v) => v.toFixed(2),
    key: 'shareRatio',
    name: 'Share Ratio',
  },
];

const DEFAULT_HISTORY_SERIES = new Set(['uploadBytes', 'downloadBytes']);

const HistoricalStatistics = ({
  activeTab,
  directories,
  exceptions,
  histogram,
  historyDays,
  historyEnd,
  historyLabel,
  historyRanges,
  historyStart,
  leaderboard,
  loading,
  onHistoryRangeSelect,
  onTabChange,
  summary,
}) => {
  const chartData = useMemo(() => buildChartData(histogram), [histogram]);

  const dlBytes = sumBytes(summary.Download ?? {});
  const ulBytes = sumBytes(summary.Upload ?? {});
  const shareRatio = dlBytes > 0 ? ulBytes / dlBytes : null;
  const shareRatioColor =
    shareRatio === null
      ? 'grey'
      : shareRatio > 0.66
        ? 'green'
        : shareRatio >= 0.33
          ? 'yellow'
          : 'red';

  const histogramTickFormatter = (timestamp) => {
    if (historyDays === 1) {
      return new Date(timestamp).toLocaleTimeString(undefined, {
        hour: '2-digit',
        minute: '2-digit',
      });
    }

    return new Date(timestamp).toLocaleDateString(undefined, {
      day: 'numeric',
      month: 'short',
    });
  };

  const historyPanes = [
    {
      menuItem: { content: 'Users', icon: 'users', key: 'users' },
      render: () => (
        <Tab.Pane>
          <Leaderboard
            downloads={leaderboard.download}
            end={historyEnd}
            start={historyStart}
            uploads={leaderboard.upload}
          />
        </Tab.Pane>
      ),
    },
    {
      menuItem: { content: 'Content', icon: 'folder open', key: 'content' },
      render: () => (
        <Tab.Pane>
          <TopDirectories rows={directories} />
        </Tab.Pane>
      ),
    },
    {
      menuItem: { content: 'Errors', icon: 'warning sign', key: 'errors' },
      render: () => (
        <Tab.Pane>
          <TransferErrors
            chartData={chartData}
            download={exceptions.download}
            historyDays={historyDays}
            upload={exceptions.upload}
          />
        </Tab.Pane>
      ),
    },
  ];

  return (
    <Segment>
      {loading && <LoaderSegment />}
      {!loading && (
        <>
          <div
            style={{
              alignItems: 'center',
              display: 'flex',
              justifyContent: 'space-between',
              marginBottom: '1em',
            }}
          >
            <Header
              as="h4"
              style={{ margin: 0 }}
            >
              <Icon name="history" />
              <Header.Content>History</Header.Content>
            </Header>
            <ButtonGroup size="mini">
              {historyRanges.map(({ label }) => (
                <Button
                  active={historyLabel === label}
                  key={label}
                  onClick={() => onHistoryRangeSelect(label)}
                >
                  {label}
                </Button>
              ))}
            </ButtonGroup>
          </div>
          <Statistic.Group
            size="small"
            widths="four"
          >
            <Statistic color="blue">
              <Statistic.Value>
                <Icon
                  name="arrow down"
                  size="tiny"
                  style={{ marginRight: '5px' }}
                />
                {formatBytesParts(sumBytes(summary.Download ?? {})).value}
                <span style={{ color: '#999', fontSize: '0.5em' }}>
                  {formatBytesParts(sumBytes(summary.Download ?? {})).unit}
                </span>
              </Statistic.Value>
              <Statistic.Label>
                Downloaded ·{' '}
                {sumCounts(summary.Download ?? {}).toLocaleString()} files
              </Statistic.Label>
            </Statistic>
            <Statistic color="green">
              <Statistic.Value>
                <Icon
                  name="arrow up"
                  size="tiny"
                  style={{ marginRight: '5px' }}
                />
                {formatBytesParts(sumBytes(summary.Upload ?? {})).value}
                <span style={{ color: '#999', fontSize: '0.5em' }}>
                  {formatBytesParts(sumBytes(summary.Upload ?? {})).unit}
                </span>
              </Statistic.Value>
              <Statistic.Label>
                Uploaded · {sumCounts(summary.Upload ?? {}).toLocaleString()}{' '}
                files
              </Statistic.Label>
            </Statistic>
            <Statistic color={shareRatioColor}>
              <Statistic.Value>
                <Icon
                  name="chart pie"
                  size="mini"
                  style={{ marginRight: '5px' }}
                />
                {shareRatio !== null ? shareRatio.toFixed(2) : '—'}
              </Statistic.Value>
              <Statistic.Label>Share ratio (↑/↓)</Statistic.Label>
            </Statistic>
            <Statistic>
              <Statistic.Value>
                <Icon
                  name="user"
                  size="mini"
                  style={{ marginRight: '5px' }}
                />
                {(
                  (summary.Upload?.Succeeded?.distinctUsers ?? 0) +
                  (summary.Download?.Succeeded?.distinctUsers ?? 0)
                ).toLocaleString()}
              </Statistic.Value>
              <Statistic.Label>Distinct peers</Statistic.Label>
            </Statistic>
          </Statistic.Group>
          <div style={{ marginTop: '1.5em' }}>
            <Graph
              data={chartData}
              defaultSeries={DEFAULT_HISTORY_SERIES}
              series={HISTORY_SERIES}
              xFormatter={histogramTickFormatter}
              yFormatter={formatBytes}
            />
          </div>
          <Divider />
          <Tab
            activeIndex={activeTab}
            onTabChange={(_, { activeIndex }) => onTabChange(activeIndex)}
            panes={historyPanes}
          />
        </>
      )}
    </Segment>
  );
};

export default HistoricalStatistics;
