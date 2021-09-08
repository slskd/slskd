import api from './api';

export const getState = async () => {
  return (await api.get('/application')).data;
};