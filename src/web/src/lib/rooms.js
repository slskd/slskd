import api from './api';

export const getAvailable = async () => {
  return (await api.get('/rooms/available')).data;
};

export const getJoined = async () => {
  return (await api.get('/rooms/joined')).data;
};

export const getMessages = async ({ roomName }) => {
  return (await api.get(`/rooms/joined/${roomName}/messages`)).data;
};

export const getUsers = async ({ roomName }) => {
  return (await api.get(`/rooms/joined/${roomName}/users`)).data;
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