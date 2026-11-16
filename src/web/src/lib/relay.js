import api from './api';

export const connect = () => {
  return api.put('/relay/agent');
};

export const disconnect = () => {
  return api.delete('/relay/agent');
};
