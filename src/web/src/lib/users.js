import api from './api';

export const getInfo = ({ username, bypassCache = false }) => {
  return api.get(`/users/${username}/info?bypassCache=${bypassCache}`);
};

export const getStatus = ({ username }) => {
  return api.get(`/users/${username}/status`);
};

export const getEndpoint = ({ username }) => {
  return api.get(`/users/${username}/endpoint`);
};

export const browse = async ({ username }) => {
  return (await api.get(`/users/${username}/browse`)).data;
};

export const getBrowseStatus = ({ username }) => {
  return api.get(`/users/${username}/browse/status`);
};