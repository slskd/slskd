import api from './api';

// paths may contain any unicode character, so they are encoded as UTF-8 before base64 (btoa only accepts Latin-1),
// and the result is URL-encoded because base64 may contain '/', which would otherwise split the route

export const list = async ({ root, subdirectory = '' }) => {
  const response = (
    await api.get(
      `/files/${root}/directories/${encodeURIComponent(btoa(String.fromCodePoint(...new TextEncoder().encode(subdirectory))))}`,
    )
  ).data;

  return response;
};

export const deleteDirectory = async ({ root, path }) => {
  const response = await api.delete(
    `/files/${root}/directories/${encodeURIComponent(btoa(String.fromCodePoint(...new TextEncoder().encode(path))))}`,
  );

  return response;
};

export const deleteFile = async ({ root, path }) => {
  const response = await api.delete(
    `/files/${root}/files/${encodeURIComponent(btoa(String.fromCodePoint(...new TextEncoder().encode(path))))}`,
  );

  return response;
};
