import api from './api';

export const browse = async ({ username }) => {
  return (await api.get(`/user/${username}/browse`)).data;
};

export const getBrowseStatus = ({ username }) => {
  return api.get(`/user/${username}/browse/status`);
};