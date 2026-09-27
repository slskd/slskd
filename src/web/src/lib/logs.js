import api from './api';

export const list = async () => {
  const response = (await api.get('/logs/files')).data;

  return response;
};

export const getFile = async ({ filename }) => {
  const response = (
    await api.get(`/logs/files/${encodeURIComponent(filename)}`, {
      responseType: 'text',
    })
  ).data;

  return response;
};

export const getFileBlob = async ({ filename }) => {
  const response = (
    await api.get(`/logs/files/${encodeURIComponent(filename)}`, {
      responseType: 'blob',
    })
  ).data;

  return response;
};
