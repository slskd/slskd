import api from './api';

export const getInfo = ({ username, bypassCache = false }) => {
  return api.get(`/peers/${username}/info?bypassCache=${bypassCache}`);
};

export const getStatus = ({ username }) => {
  return api.get(`/peers/${username}/status`);
};

export const getEndpoint = ({ username }) => {
  return api.get(`/peers/${username}/endpoint`);
};

export const browse = async ({ username }) => {
  return (await api.get(`/peers/${username}/browse`)).data;
};

export const getBrowseStatus = ({ username }) => {
  return api.get(`/peers/${username}/browse/status`);
};