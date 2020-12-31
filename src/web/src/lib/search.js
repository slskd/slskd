import api from './api';

export const search = ({ id, searchText }) => {
  return api.post(`/searches`, { id, searchText });
};

export const getStatus = async ({ id }) => {
  return (await api.get(`/searches/${encodeURIComponent(id)}`)).data;
};