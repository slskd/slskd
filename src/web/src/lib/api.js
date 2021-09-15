import axios from 'axios';
import { baseUrl, tokenKey, tokenPassthroughValue } from '../config';

const getToken = () => {
  return JSON.parse(sessionStorage.getItem(tokenKey) || localStorage.getItem(tokenKey));
}

axios.defaults.baseURL = baseUrl;
const api = axios.create();

api.interceptors.request.use(config => {
    const token = getToken();

    config.headers['Content-Type'] = 'application/json';

    if (token && token !== tokenPassthroughValue) {
        config.headers.Authorization = 'Bearer ' + token;
    }

    return config;
});

api.interceptors.response.use(response => {
  return response;
}, error => {
  if (error.response.status === 401 && !['/session', '/server', '/application'].includes(error.response.config.url)) {
    sessionStorage.removeItem(tokenKey);
    localStorage.removeItem(tokenKey);

    window.location.reload(true);

    return Promise.reject(error);
  } 
  else {
      return Promise.reject(error);
  }
});

export default api;