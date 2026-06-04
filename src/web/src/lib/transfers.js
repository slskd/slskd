import api from './api';

export const getAll = async ({ direction }) => {
  const response = (
    await api.get(`/transfers/${encodeURIComponent(direction)}s`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from transfers API', response);
    return undefined;
  }

  return response;
};

export const download = ({ username, files = [] }) => {
  return api.post(
    `/transfers/downloads/${encodeURIComponent(username)}`,
    files,
  );
};

export const cancel = ({ direction, username, id, remove = false }) => {
  return api.delete(
    `/transfers/${direction}s/${encodeURIComponent(username)}/${encodeURIComponent(id)}?remove=${remove}`,
  );
};

export const clearCompleted = ({ direction }) => {
  return api.delete(`/transfers/${direction}s/all/completed`);
};

// 'Requested'
// 'Queued, Remotely'
// 'Queued, Locally'
// 'Initializing'
// 'InProgress'
// 'Completed, Succeeded'
// 'Completed, Cancelled'
// 'Completed, TimedOut'
// 'Completed, Errored'
// 'Completed, Rejected'

export const getPlaceInQueue = ({ username, id }) => {
  return api.get(
    `/transfers/downloads/${encodeURIComponent(username)}/${encodeURIComponent(id)}/position`,
  );
};

export const isStateRetryable = (state) =>
  state.includes('Completed') && state !== 'Completed, Succeeded';

export const isStateCancellable = (state) =>
  [
    'InProgress',
    'Requested',
    'Queued',
    'Queued, Remotely',
    'Queued, Locally',
    'Initializing',
  ].find((s) => s === state);

export const isStateRemovable = (state) => state.includes('Completed');

/**
 * @typedef {object} TransferSummary
 * @property {number} totalBytes - Total number of bytes transferred
 * @property {number} count - Number of transfers
 * @property {number} distinctUsers - Number of distinct users
 * @property {number} averageSpeed - Average transfer speed
 * @property {number} averageWait - Average wait time
 * @property {number} averageDuration - Average transfer duration
 * @typedef {object} TransferStateSummaries
 * @property {TransferSummary} Aborted - Aborted transfers
 * @property {TransferSummary} Cancelled - Cancelled transfers
 * @property {TransferSummary} Errored - Errored transfers
 * @property {TransferSummary} Rejected - Rejected transfers
 * @property {TransferSummary} Succeeded - Successful transferse
 * @returns {Promise<{Upload: TransferStateSummaries, Download: TransferStateSummaries}>}
 */
export const getStats = () => {
  const startTime = '0001-01-01 00:00:00Z';
  return api.get(
    `/telemetry/reports/transfers/summary?start=${encodeURIComponent(startTime)}`,
  );
};
