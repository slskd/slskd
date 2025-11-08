import * as optionsApi from './options';
import { toast } from 'react-toastify';
import YAML from 'yaml';

/**
 * Navigate to user profile page
 * @param {Object} history - React Router history object
 * @param {string} username - Username to view
 */
export const navigateToUserProfile = (history, username) => {
  history.push('/users', { user: username });
};

/**
 * Navigate to user browse shares page
 * @param {Object} history - React Router history object
 * @param {string} username - Username to browse
 */
export const navigateToBrowseShares = (history, username) => {
  history.push('/browse', { user: username });
};

/**
 * Add user to blacklist via YAML configuration
 * @param {string} username - Username to ignore
 * @returns {Promise<boolean>} Success status
 */
export const ignoreUser = async (username) => {
  if (!username) {
    toast.error('No username provided');
    return false;
  }

  try {
    const yamlText = await optionsApi.getYaml();
    const yamlDocument = YAML.parseDocument(yamlText);

    let groups = yamlDocument.get('groups');

    if (!groups) {
      groups = YAML.createNode({});
      yamlDocument.set('groups', groups);
    }

    let blacklisted = groups.get('blacklisted');

    if (!blacklisted) {
      blacklisted = YAML.createNode({});
      groups.set('blacklisted', blacklisted);
    }

    let members = blacklisted.get('members');

    if (!members) {
      members = YAML.createNode([]);
      blacklisted.set('members', members);
    }

    if (!members.items.includes(username)) {
      members.add(username);
    }

    const newYamlText = yamlDocument.toString();
    await optionsApi.updateYaml({ yaml: newYamlText });
    toast.success(`User '${username}' added to blacklist.`);
    return true;
  } catch (error) {
    toast.error('Failed to ignore user: ' + error);
    return false;
  }
};
