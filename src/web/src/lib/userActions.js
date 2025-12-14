import * as optionsApi from './options';
import { toast } from 'react-toastify';
import YAML from 'yaml';

/**
 * Navigate to user profile page
 * @param {object} history - React Router history object
 * @param {string} username - Username to view
 */
export const navigateToUserProfile = (history, username) => {
  history.push('/users', { user: username });
};

/**
 * Navigate to user browse shares page
 * @param {object} history - React Router history object
 * @param {string} username - Username to browse
 */
export const navigateToBrowseShares = (history, username) => {
  history.push('/browse', { user: username });
};

/**
 * Add user to blacklist via YAML configuration
 * Uses CST (Concrete Syntax Tree) to preserve exact whitespace and formatting
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

    const parser = new YAML.Parser();
    const tokens = Array.from(parser.parse(yamlText));
    const cstDocument = tokens[0];

    if (!cstDocument || !cstDocument.value) {
      toast.error('Failed to parse options YAML');
      return false;
    }

    let userAdded = false;

    YAML.CST.visit(cstDocument, (item) => {
      if (item.key && YAML.CST.isScalar(item.key)) {
        const keyResolved = YAML.CST.resolveAsScalar(item.key);

        if (keyResolved?.value === 'members' && item.value?.items) {
          const items = item.value.items;
          const existingUser = items.some((seqItem) => {
            if (seqItem.value && YAML.CST.isScalar(seqItem.value)) {
              const resolved = YAML.CST.resolveAsScalar(seqItem.value);
              return resolved?.value === username;
            }

            return false;
          });

          if (existingUser) {
            toast.info(`User '${username}' is already in the blacklist.`);
            return YAML.CST.visit.BREAK;
          }

          const lastItem = items[items.length - 1];

          let indent = 6;
          let spaces = '      ';

          if (lastItem?.start) {
            const spaceToken = lastItem.start.find((t) => t.type === 'space');
            if (spaceToken) {
              spaces = spaceToken.source;
              indent = spaceToken.source.length - 2;
            }
          }

          items.push({
            start: [
              { indent: 0, offset: -1, source: '\n', type: 'newline' },
              { indent: 0, offset: -1, source: spaces, type: 'space' },
              { indent: 0, offset: -1, source: '-', type: 'seq-item-ind' },
              { indent: 0, offset: -1, source: ' ', type: 'space' },
            ],
            value: { indent, offset: -1, source: username, type: 'scalar' },
          });

          userAdded = true;
          return YAML.CST.visit.BREAK;
        }
      }

      return undefined;
    });

    if (!userAdded) {
      toast.error(
        'Could not find groups.blacklisted.members in YAML configuration. Please add the structure manually first.',
      );
      return false;
    }

    const newYamlText = YAML.CST.stringify(cstDocument);
    await optionsApi.updateYaml({ yaml: newYamlText });
    toast.success(`User '${username}' added to blacklist.`);
    return true;
  } catch (error) {
    toast.error('Failed to ignore user: ' + error);
    console.error('Error ignoring user:', error);
    return false;
  }
};
