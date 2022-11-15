import api from './api';

export const connect = () => {
  return api.put('/relay');
};

export const disconnect = () => {
  return api.delete('/relay');
};