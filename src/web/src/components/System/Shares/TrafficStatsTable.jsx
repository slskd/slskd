import { Table } from 'semantic-ui-react';

const formatBytes = (bytes, decimals = 2) => {
  if (!Number(bytes)) return '0 Bytes';

  const k = 1_024;
  const dm = Math.max(0, decimals);
  const sizes = [
    'Bytes',
    'KiB',
    'MiB',
    'GiB',
    'TiB',
    'PiB',
    'EiB',
    'ZiB',
    'YiB',
  ];

  const index = Math.floor(Math.log(bytes) / Math.log(k));
  const scaled = (bytes / k ** index).toFixed(dm);
  const unit = sizes[index];

  return `${scaled} ${unit}`;
};

/**
 * @typedef {object} TrafficStats
 * @property {number} uploadedFiles - Number of successfully uploaded files
 * @property {number} uploadedBytes - Number of bytes uploaded
 * @property {number} downloadedFiles - Number of successfully downloaded files
 * @property {number} downloadedBytes - Number of bytes downloaded
 * @param {object} params
 * @param {TrafficStats} params.stats
 */
const TrafficStatsTable = ({ stats }) => {
  return (
    <Table>
      <Table.Header>
        <Table.Row>
          <Table.HeaderCell colSpan="2">Traffic Statistics</Table.HeaderCell>
        </Table.Row>
      </Table.Header>
      <Table.Body>
        <Table.Row>
          <Table.Cell>
            Uploaded (Completed) Files: {stats.uploadedFiles}
          </Table.Cell>
          <Table.Cell>
            Total Upload Traffic: {formatBytes(stats.uploadedBytes)}
          </Table.Cell>
        </Table.Row>
        <Table.Row>
          <Table.Cell>
            Downloaded (Completed) Files: {stats.downloadedFiles}
          </Table.Cell>
          <Table.Cell>
            Total Download Traffic: {formatBytes(stats.downloadedBytes)}
          </Table.Cell>
        </Table.Row>
      </Table.Body>
    </Table>
  );
};

export default TrafficStatsTable;
