import api from './api';

export const getLogFiles = async () => {
  const response = (await api.get('/logs/files')).data;

  return response;
};

export const getLogFile = async ({ filename }) => {
  const response = (
    await api.get(`/logs/files/${encodeURIComponent(filename)}`)
  ).data;

  // log records carry nothing unique; timestamps are second precision and the
  // same line can repeat within a second.  tag each with its position in the
  // file so the UI has a stable, unique key to render and sort with.
  return response.map((record, index) => ({ ...record, id: index }));
};

export const downloadLogFile = async ({ filename }) => {
  const response = (
    await api.get(`/logs/files/${encodeURIComponent(filename)}`, {
      params: { download: true },
      responseType: 'blob',
    })
  ).data;

  return response;
};
