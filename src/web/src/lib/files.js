import api from './api';

export const list = async ({ root, subdirectory = '' }) => {
  subdirectory = btoa(subdirectory);
  const response = (await api.get(`/files/${root}/directories/${subdirectory}`)).data;

  return response;
};

export const deleteDirectory = async ({ root, path }) => {
  path = btoa(path);
  const response = (await api.delete(`/files/${root}/directories/${path}`));

  return response;
};

export const deleteFile = async ({ root, path }) => {
  path = btoa(path);
  const response = (await api.delete(`/files/${root}/files/${path}`));

  return response;
};