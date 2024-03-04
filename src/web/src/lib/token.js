import { tokenKey, tokenPassthroughValue } from '../config';

export const getToken = () =>
  sessionStorage.getItem(tokenKey) || localStorage.getItem(tokenKey);
export const setToken = (storage, token) => storage.setItem(tokenKey, token);
export const clearToken = () => {
  localStorage.removeItem(tokenKey);
  sessionStorage.removeItem(tokenKey);
};

export const isPassthroughEnabled = () => getToken() === tokenPassthroughValue;
