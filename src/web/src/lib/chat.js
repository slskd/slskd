import api from './api';

export const getAll = async () => {
  const response = (await api.get('/conversations')).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from conversations API', response)
    return undefined;
  }

  return response;
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