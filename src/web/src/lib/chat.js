import api from './api';

export const getAll = async () => {
  return (await api.get('/conversations')).data;
};

export const acknowledge = ({ username }) => {
  return api.put(`/conversations/${username}`);
};

export const send = ({ username, message }) => {
  return api.post(`/conversations/${username}`, JSON.stringify(message));
};

export const remove = ({ username }) => {
  return api.delete(`/conversations/${username}`);
};