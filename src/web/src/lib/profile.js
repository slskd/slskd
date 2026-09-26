import api from './api';

// resolves an object url for the configured picture, or undefined if there isn't one.
// the caller owns the url and should revoke it when it's no longer displayed
export const getPictureUrl = async () => {
  try {
    const response = await api.get('/profile/picture', {
      responseType: 'blob',
    });
    return URL.createObjectURL(response.data);
  } catch (error) {
    if (error?.response?.status === 404) {
      return undefined;
    }

    throw error;
  }
};

const readAsBase64 = (file) =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    // strip the data:<mime>;base64, prefix
    reader.onload = () => resolve(String(reader.result).split(',')[1]);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });

// saves the picture on the server and resolves the path to use for soulseek.picture
export const uploadPicture = async ({ file }) => {
  const data = await readAsBase64(file);
  return (await api.put('/profile/picture', { data })).data;
};

export const maxPictureBytes = 5 * 1_024 * 1_024;
