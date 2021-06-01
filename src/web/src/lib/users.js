import api from './api';

export const getInfo = ({ username }) => {
  return api.get(`/peers/${username}/info`);
};

export const getStatus = ({ username }) => {
  return api.get(`/peers/${username}/status`);
};

export const getEndpoint = ({ username }) => {
  return api.get(`/peers/${username}/endpoint`);
};