import api from './api';

export const list = async ({ root, subdirectory = '' }) => {
  subdirectory = btoa(subdirectory);
  const response = (await api.get(`/files/${root}/directories/${subdirectory}`)).data;

  return response;
};