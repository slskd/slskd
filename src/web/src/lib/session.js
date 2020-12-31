import api from './api';

export const getSecurityEnabled = async () => {
  return (await api.get('/session/enabled')).data;
};

export const check = () => {
  return api.get('/session');
};

export const login = ({ username, password }) => {
  return api.post('/session', { username, password });
};