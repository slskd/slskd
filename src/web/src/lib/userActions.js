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

    const groupsPair = cstDocument.value.items?.find((item) => {
      if (item.key && YAML.CST.isScalar(item.key)) {
        const key = YAML.CST.resolveAsScalar(item.key);
        return key?.value === 'groups';
      }

      return false;
    });

    const blacklistedPair = groupsPair?.value?.items?.find((item) => {
      if (item.key && YAML.CST.isScalar(item.key)) {
        const key = YAML.CST.resolveAsScalar(item.key);
        return key?.value === 'blacklisted';
      }

      return false;
    });

    const membersPair = blacklistedPair?.value?.items?.find((item) => {
      if (item.key && YAML.CST.isScalar(item.key)) {
        const key = YAML.CST.resolveAsScalar(item.key);
        return key?.value === 'members';
      }

      return false;
    });

    if (!membersPair?.value?.items) {
      toast.error(
        'Could not find groups.blacklisted.members in YAML configuration. Please add the structure manually first.',
      );
      return false;
    }

    const items = membersPair.value.items;

    const existingUser = items.some((seqItem) => {
      if (seqItem.value && YAML.CST.isScalar(seqItem.value)) {
        const resolved = YAML.CST.resolveAsScalar(seqItem.value);
        return resolved?.value === username;
      }

      return false;
    });

    if (existingUser) {
      toast.info(`User '${username}' is already in the blacklist.`);
      return false;
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

    if (lastItem.value) {
      if (!lastItem.value.end) {
        lastItem.value.end = [];
      }

      if (!lastItem.value.end.some((t) => t.type === 'newline')) {
        lastItem.value.end.push({
          indent: indent + 2,
          offset: -1,
          source: '\n',
          type: 'newline',
        });
      }
    }

    items.push({
      start: [
        { indent: 0, offset: -1, source: spaces, type: 'space' },
        { indent, offset: -1, source: '-', type: 'seq-item-ind' },
        { indent: indent + 1, offset: -1, source: ' ', type: 'space' },
      ],
      value: {
        end: [
          {
            indent: indent + 2,
            offset: -1,
            source: '\n',
            type: 'newline',
          },
        ],
        indent: indent + 2,
        offset: -1,
        source: username,
        type: 'scalar',
      },
    });

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
