import api from './api';

export const connect = () => {
  return api.put('/network');
};

export const disconnect = () => {
  return api.delete('/network');
};