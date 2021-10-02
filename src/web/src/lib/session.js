import api from './api';
import { tokenKey, tokenPassthroughValue } from '../config';

export const getToken = () => JSON.parse(sessionStorage.getItem(tokenKey) || localStorage.getItem(tokenKey));
const setToken = (storage, token) => storage.setItem(tokenKey, JSON.stringify(token));

export const getSecurityEnabled = async () => {
  return (await api.get('/session/enabled')).data;
};

export const enablePassthrough = () => {
  console.debug('enabling token passthrough.  api calls will not be authenticated')
  setToken(sessionStorage, tokenPassthroughValue)
}

export const isPassthroughEnabled = () => getToken() === tokenPassthroughValue;

export const isLoggedIn = () => {
  const token = getToken();
  return token !== undefined && token !== tokenPassthroughValue;
} 

export const login = async ({ username, password, rememberMe = false }) => {
  const { token } = (await api.post('/session', { username, password })).data;
  setToken(rememberMe ? localStorage : sessionStorage, token);
  return token;
};

export const logout = () => {
  console.debug('removing token from local and session storage')
  localStorage.removeItem(tokenKey)
  sessionStorage.removeItem(tokenKey)
}

export const check = async () => {
  try {
    await api.get('/session');
  } catch (error) {
    console.error(error)
    logout();
  }
};