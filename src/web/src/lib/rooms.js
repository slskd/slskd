import api from './api';

export const getAvailable = async () => {
  const response = (await api.get('/rooms/available')).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from rooms API', response)
    return undefined;
  }

  return response;
};

export const getJoined = async () => {
  const response = (await api.get('/rooms/joined')).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from rooms API', response)
    return undefined;
  }

  return response;
};

export const getMessages = async ({ roomName }) => {
  const response = (await api.get(`/rooms/joined/${roomName}/messages`)).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from rooms API', response)
    return undefined;
  }

  return response;
};

export const getUsers = async ({ roomName }) => {
  const response = (await api.get(`/rooms/joined/${roomName}/users`)).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from rooms API', response)
    return undefined;
  }

  return response;
};

export const join = async ({ roomName }) => {
  return api.post(`/rooms/joined/${roomName}`);
};

export const leave = async ({ roomName }) => {
  return api.delete(`/rooms/joined/${roomName}`);
};

export const sendMessage = async ({ roomName, message }) => {
  return api.post(`/rooms/joined/${roomName}/messages`, JSON.stringify(message));
};