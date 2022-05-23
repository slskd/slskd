import api from './api';

export const getAll = async ({ direction }) => {
  const response = (await api.get(`/transfers/${direction}s`)).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from transfers API', response)
    return undefined;
  }

  return response;
};

export const download = ({ username, files = [] }) => {
  return api.post(`/transfers/downloads/${username}`, files);
};

export const cancel = ({ direction, username, id, remove = false }) => {
  return api.delete(`/transfers/${direction}s/${username}/${id}?remove=${remove}`);
};

export const getPlaceInQueue = ({ username, id }) => {
  return api.get(`/transfers/downloads/${username}/${id}`);
};

export const isStateRetryable = (state) =>
  state.includes('Completed') && state !== 'Completed, Succeeded';

export const isStateCancellable = (state) =>
  ['InProgress', 'Requested', 'Queued', 'Queued, Remotely', 'Queued, Locally', 'Initializing'].find(s => s === state);

export const isStateRemovable = (state) => state.includes('Completed');

/**
 * Accepts the username/file dictionary returned by the API and returns an array of files
 * reduced from the map
 */
export const reduceTransfersToFiles = (transfers) => {
  return transfers.reduce((acc, username) => {
    const allUserFiles = username.directories.reduce((acc, directory) => {
      acc = acc.concat(directory.files);
      return acc;
    }, []);
  
    acc = acc.concat(allUserFiles)
    return acc;
  }, [])
}