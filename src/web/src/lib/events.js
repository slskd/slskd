import api from './api';

export const list = async ({ offset, limit }) => {
  const response = await api.get(`/events?offset=${offset}&limit=${limit}`);

  const events = response.data;
  const totalCount = response.headers['x-total-count'];

  return { events, totalCount };
};
