import api from './api';

export const getAll = async () => {
  return (await api.get('/shares')).data;
};

export const get = async ({ id } = {}) => {
  if (!id) throw new Error('unable to get share: id is missing');
  return (await api.get(`/shares/${id}`)).data;
};

export const browseAll = async () => {
  return (await api.get('/shares/contents')).data;
};

export const browse = async ({ id } = {}) => {
  if (!id) throw new Error('unable to get share contents: id is missing');
  return (await api.get(`/shares/${id}/contents`)).data;
};