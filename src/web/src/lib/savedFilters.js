/**
 * Utility functions for managing saved search filters in localStorage
 */

const STORAGE_KEY = 'slskd_saved_filters';
const DEFAULT_FILTER_KEY = 'slskd_default_filter';

/**
 * Get all saved filters from localStorage
 * @returns {Array} Array of saved filter objects
 */
export const getSavedFilters = () => {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    return saved ? JSON.parse(saved) : [];
  } catch (error) {
    console.error('Error loading saved filters:', error);
    if (error instanceof SyntaxError) {
      throw new TypeError(
        'Saved filters data is corrupted. Please clear your browser data.',
      );
    }

    throw new Error('Failed to access browser storage.' + error.message);
  }
};

/**
 * Create a new filter
 * @param {string} name - Name of the filter
 * @param {string} filterString - Filter string to save
 * @returns {boolean} Success status
 */
export const createFilter = (name, filterString) => {
  try {
    const filters = getSavedFilters();
    filters.push({
      createdAt: new Date().toISOString(),
      filterString,
      lastUsed: new Date().toISOString(),
      name,
    });
    localStorage.setItem(STORAGE_KEY, JSON.stringify(filters));
    return true;
  } catch (error) {
    console.error('Error creating filter:', error);
    throw new Error('Failed to create filter.' + error.message);
  }
};

/**
 * Delete a saved filter
 * @param {string} name - Name of the filter to delete
 * @returns {boolean} Success status
 */
export const deleteFilter = (name) => {
  try {
    const filters = getSavedFilters();
    const filteredFilters = filters.filter((f) => f.name !== name);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(filteredFilters));
    return true;
  } catch (error) {
    console.error('Error deleting filter:', error);
    throw new Error('Failed to delete filter.' + error.message);
  }
};

/**
 * Get a specific saved filter by name
 * @param {string} name - Name of the filter to retrieve
 * @returns {object | null} Filter object or null if not found
 */
export const getFilter = (name) => {
  const filters = getSavedFilters();
  return filters.find((f) => f.name === name) || null;
};

/**
 * Update the last used timestamp for a filter
 * @param {string} name - Name of the filter
 */
export const markFilterAsUsed = (name) => {
  try {
    const filters = getSavedFilters();
    const filterIndex = filters.findIndex((f) => f.name === name);

    if (filterIndex >= 0) {
      filters[filterIndex].lastUsed = new Date().toISOString();
      localStorage.setItem(STORAGE_KEY, JSON.stringify(filters));
    }
  } catch (error) {
    console.error('Error updating filter usage:', error);
    throw new Error('Failed to update filter usage.' + error.message);
  }
};

/**
 * Set a filter as the default filter
 * @param {string} filterName - Name of the filter to set as default
 * @returns {boolean} Success status
 */
export const setDefaultFilter = (filterName) => {
  try {
    const filters = getSavedFilters();
    const filter = filters.find((f) => f.name === filterName);

    if (!filter) {
      return false;
    }

    localStorage.setItem(DEFAULT_FILTER_KEY, filterName);
    return true;
  } catch (error) {
    console.error('Error setting default filter:', error);
    throw new Error('Failed to set default filter.' + error.message);
  }
};

/**
 * Get the current default filter
 * @returns {object | null} Default filter object or null if none set
 */
export const getDefaultFilter = () => {
  try {
    const defaultFilterName = localStorage.getItem(DEFAULT_FILTER_KEY);

    if (!defaultFilterName) {
      return null;
    }

    return getFilter(defaultFilterName);
  } catch (error) {
    console.error('Error getting default filter:', error);
    throw new Error('Failed to get default filter.' + error.message);
  }
};

/**
 * Clear the default filter setting
 * @returns {boolean} Success status
 */
export const clearDefaultFilter = () => {
  try {
    localStorage.removeItem(DEFAULT_FILTER_KEY);
    return true;
  } catch (error) {
    console.error('Error clearing default filter:', error);
    throw new Error('Failed to clear default filter.' + error.message);
  }
};

/**
 * Check if a filter is set as default
 * @param {string} filterName - Name of the filter to check
 * @returns {boolean} True if the filter is the default
 */
export const isDefaultFilter = (filterName) => {
  try {
    const defaultFilterName = localStorage.getItem(DEFAULT_FILTER_KEY);
    return defaultFilterName === filterName;
  } catch (error) {
    console.error('Error checking default filter:', error);
    throw new Error('Failed to check default filter.' + error.message);
  }
};
