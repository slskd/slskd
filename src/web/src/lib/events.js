import api from './api';

export const list = async ({ offset, limit }) => {
  const response = (await api.get(`/events?offset=${offset}&limit=${limit}`))
    .data;
  return response;
};
