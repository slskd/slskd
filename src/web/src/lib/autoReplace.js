import api from './api';

/**
 * Get all stuck downloads that are candidates for auto-replacement.
 * @returns {Promise<Array>} List of stuck downloads.
 */
export const getStuckDownloads = async () => {
  const response = await api.get('/transfers/downloads/stuck');
  return response.data;
};

/**
 * Find alternative sources for a specific stuck download.
 * @param {object} params - The parameters.
 * @param {string} params.username - The username of the original source.
 * @param {string} params.filename - The full filename/path.
 * @param {number} params.size - The expected file size.
 * @param {number} params.threshold - Max size difference percentage (e.g., 5 for 5%).
 * @returns {Promise<Array>} List of alternative candidates.
 */
export const findAlternative = async ({
  filename,
  size,
  threshold = 5,
  username,
}) => {
  const response = await api.post('/transfers/downloads/find-alternative', {
    filename,
    size,
    threshold,
    username,
  });
  return response.data;
};

/**
 * Replace a stuck download with an alternative source.
 * @param {object} params - The parameters.
 * @param {string} params.originalId - The ID of the stuck download to replace.
 * @param {string} params.originalUsername - The username of the original source.
 * @param {string} params.newUsername - The username of the alternative source.
 * @param {string} params.newFilename - The filename from the alternative source.
 * @param {number} params.newSize - The size of the alternative file.
 * @returns {Promise<object>} The replacement result.
 */
export const replaceDownload = async ({
  newFilename,
  newSize,
  newUsername,
  originalId,
  originalUsername,
}) => {
  const response = await api.post('/transfers/downloads/replace', {
    newFilename,
    newSize,
    newUsername,
    originalId,
    originalUsername,
  });
  return response.data;
};

/**
 * Process all stuck downloads and attempt auto-replacement.
 * @param {object} params - The parameters.
 * @param {number} params.threshold - Max size difference percentage for auto-replacement.
 * @returns {Promise<object>} The auto-replace result with counts.
 */
export const processStuckDownloads = async ({ threshold = 5 }) => {
  const response = await api.post('/transfers/downloads/auto-replace', {
    threshold,
  });
  return response.data;
};

/**
 * Get auto-replace configuration/status.
 * @returns {Promise<object>} The auto-replace status.
 */
export const getAutoReplaceStatus = async () => {
  const response = await api.get('/transfers/downloads/auto-replace/status');
  return response.data;
};
