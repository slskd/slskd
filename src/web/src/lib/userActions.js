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
 * Ensure sequence item value ends with a newline token
 * @param {object} seqItem - CST sequence item
 * @param {number} newlineIndent - Indentation for newline token
 */
const ensureValueEndsWithNewline = (seqItem, newlineIndent) => {
  if (!seqItem?.value) return;

  seqItem.value.end ??= [];

  const hasNewline = seqItem.value.end.some((t) => t.type === 'newline');
  if (!hasNewline) {
    seqItem.value.end.push({
      indent: newlineIndent,
      offset: -1,
      source: '\n',
      type: 'newline',
    });
  }
};

export const addUserToBlacklist = (yamlText, username) => {
  const parser = new YAML.Parser();
  let cstDocument;

  for (const token of parser.parse(yamlText)) {
    if (token.type === 'document') {
      cstDocument = token;
      break;
    }
  }

  if (!cstDocument || !cstDocument.value) {
    toast.error('Failed to parse options YAML');
    return false;
  }

  const transfersPair = cstDocument.value.items?.find((item) => {
    if (item.key && YAML.CST.isScalar(item.key)) {
      const key = YAML.CST.resolveAsScalar(item.key);
      return key?.value === 'transfers';
    }

    return false;
  });

  const groupsPair = transfersPair?.value?.items?.find((item) => {
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

  if (
    membersPair?.value?.type === 'flow-collection' &&
    membersPair.value.start?.source === '[' &&
    membersPair.value.end?.some((token) => token.source === ']')
  ) {
    const keyIndent = membersPair.key?.indent ?? 4;
    membersPair.sep = [
      { indent: keyIndent, offset: -1, source: ':', type: 'map-value-ind' },
      { indent: keyIndent + 1, offset: -1, source: '\n', type: 'newline' },
    ];
    membersPair.value = {
      indent: keyIndent + 2,
      items: [],
      offset: -1,
      type: 'block-seq',
    };
  }

  if (!membersPair?.value?.items) {
    toast.error(
      'Could not find transfers.groups.blacklisted.members in YAML configuration. Please add the structure manually first.',
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

  const lastItem = items.at(-1);

  let startTokens;
  let valueIndent;

  if (lastItem) {
    const lastValueToken = lastItem.value;
    valueIndent = lastValueToken?.indent ?? 8;
    const seqIndent = Math.max(valueIndent - 2, 0);
    const spaces = ' '.repeat(seqIndent);
    startTokens = [
      { indent: 0, offset: -1, source: spaces, type: 'space' },
      { indent: seqIndent, offset: -1, source: '-', type: 'seq-item-ind' },
      { indent: seqIndent + 1, offset: -1, source: ' ', type: 'space' },
    ];
    ensureValueEndsWithNewline(lastItem, valueIndent);
  } else {
    const parentIndent = membersPair.key?.indent ?? 4;
    const spaces = ' '.repeat(parentIndent + 2);
    startTokens = [
      { indent: 0, offset: -1, source: spaces, type: 'space' },
      {
        indent: parentIndent + 2,
        offset: -1,
        source: '-',
        type: 'seq-item-ind',
      },
      { indent: parentIndent + 3, offset: -1, source: ' ', type: 'space' },
    ];
    valueIndent = parentIndent + 4;
  }

  const valueToken = YAML.CST.createScalarToken(username, {
    end: [{ indent: valueIndent, offset: -1, source: '\n', type: 'newline' }],
    indent: valueIndent,
    inFlow: false,
    type: 'QUOTE_DOUBLE',
  });

  items.push({
    start: startTokens,
    value: valueToken,
  });

  const newYamlText = YAML.CST.stringify(cstDocument);
  const prefix = yamlText.slice(0, cstDocument.offset ?? 0);
  return `${prefix}${newYamlText}`;
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

    const newYamlText = addUserToBlacklist(yamlText, username);
    if (!newYamlText) {
      return false;
    }

    await optionsApi.updateYaml({ yaml: newYamlText });
    toast.success(`User '${username}' added to blacklist.`);
    return true;
  } catch (error) {
    toast.error('Failed to ignore user: ' + error);
    console.error('Error ignoring user:', error);
    return false;
  }
};
