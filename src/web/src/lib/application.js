import api from './api';

export const getState = async () => {
  return (await api.get('/application')).data;
};

export const restart = async () => {
  return api.put('/application');
};

export const shutdown = async () => {
  return api.delete('/application');
};

export const getVersion = async ({ forceCheck = false }) => {
  return (await api.get(`/application/version/latest?forceCheck=${forceCheck}`)).data;
};