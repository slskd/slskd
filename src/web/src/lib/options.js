import api from './api';

export const getCurrent = async () => {
  return (await api.get('/options')).data;
};

export const getCurrentDebugView = async () => {
  return (await api.get('/options/debug')).data;
};

export const getYaml = async () => {
  return (await api.get('/options/yaml')).data;
};

export const getYamlLocation = async () => {
  return (await api.get('/options/yaml/location')).data;
};

export const validateYaml = async ({ yaml }) => {
  return (await api.post('/options/yaml/validate', yaml)).data;
};

export const updateYaml = async ({ yaml }) => {
  return (await api.post('/options/yaml', yaml)).data;
};