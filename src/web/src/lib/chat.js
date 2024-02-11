import api from './api';

export const getAll = async () => {
  return (await api.get('/conversations')).data;
};

export const get = async ({ username }) => {
  return (await api.get(`/conversations/${encodeURIComponent(username)}`)).data;
};

export const acknowledge = ({ username }) => {
  return api.put(`/conversations/${encodeURIComponent(username)}`);
};

export const send = ({ username, message }) => {
  return api.post(
    `/conversations/${encodeURIComponent(username)}`,
    JSON.stringify(message),
  );
};

export const remove = ({ username }) => {
  return api.delete(`/conversations/${encodeURIComponent(username)}`);
};
