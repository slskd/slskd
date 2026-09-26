import api from './api';

// the server stores interests trimmed and lowercased; so do other clients
export const normalize = (item) => (item ?? '').trim().toLowerCase();

export const getUsersWhoLike = async ({ item }) => {
  return (await api.get('/interests/users', { params: { item } })).data;
};

export const getSimilarUsers = async () => {
  return (await api.get('/interests/users/similar')).data;
};

export const getRecommendations = async ({ item } = {}) => {
  return (
    await api.get('/interests/recommendations', {
      params: item ? { item } : undefined,
    })
  ).data;
};

export const getGlobalRecommendations = async () => {
  return (await api.get('/interests/recommendations/global')).data;
};
