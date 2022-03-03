import axios from 'axios';
import { baseUrl } from '../config';
import * as session from './session';

axios.defaults.baseURL = baseUrl;

const api = axios.create({
  withCredentials: true
});

api.interceptors.request.use(config => {
    const token = session.getToken();

    config.headers['Content-Type'] = 'application/json';

    if (!session.isPassthroughEnabled() && token) {
        config.headers.Authorization = 'Bearer ' + token;
    }

    return config;
});

api.interceptors.response.use(response => {
  return response;
}, error => {
  if (error.response.status === 401 && !['/session', '/server', '/application'].includes(error.response.config.url)) {
    console.debug('received 401 from api route, logging out')
    session.logout();
    window.location.reload(true);

    return Promise.reject(error);
  } 
  else {
      return Promise.reject(error);
  }
});

export default api;