import api from './api';

export const search = ({ id, searchText }) => {
  return api.post(`/searches`, { id, searchText });
};

export const getStatus = async ({ id, includeResponses = false }) => {
  return (await api.get(`/searches/${encodeURIComponent(id)}?includeResponses=${includeResponses}`)).data;
};

export const getResponses = async ({ id }) => {
  return (await api.get(`/searches/${encodeURIComponent(id)}`)).data;
};