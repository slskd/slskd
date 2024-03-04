import api from './api';

export const list = async ({ root, subdirectory = '' }) => {
  const response = (
    await api.get(`/files/${root}/directories/${btoa(subdirectory)}`)
  ).data;

  return response;
};

export const deleteDirectory = async ({ root, path }) => {
  const response = await api.delete(`/files/${root}/directories/${btoa(path)}`);

  return response;
};

export const deleteFile = async ({ root, path }) => {
  const response = await api.delete(`/files/${root}/files/${btoa(path)}`);

  return response;
};
