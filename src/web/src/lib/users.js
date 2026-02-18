import api from './api';

export const getInfo = ({ username }) => {
  return api.get(`/users/${encodeURIComponent(username)}/info`);
};

export const getStatus = ({ username }) => {
  return api.get(`/users/${encodeURIComponent(username)}/status`);
};

export const getEndpoint = ({ username }) => {
  return api.get(`/users/${encodeURIComponent(username)}/endpoint`);
};

export const browse = async ({ username }) => {
  return (await api.get(`/users/${encodeURIComponent(username)}/browse`)).data;
};

export const getBrowseStatus = ({ username }) => {
  return api.get(`/users/${encodeURIComponent(username)}/browse/status`);
};

export const getDirectoryContents = async ({ username, directory }) => {
  return (
    await api.post(`/users/${encodeURIComponent(username)}/directory`, {
      directory,
    })
  ).data;
};

export const setPresence = async ({ presence }) => {
  return await api.put(`/users/presence`, presence);
};