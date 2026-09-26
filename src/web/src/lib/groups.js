// user bans and group membership, stored in the configuration file
import {
  addToList,
  removeFromList,
  updateConfiguration,
} from './configuration';

const blacklistPath = ['transfers', 'groups', 'blacklisted', 'members'];
const groupMembersPath = (group) => [
  'transfers',
  'groups',
  'userDefined',
  group,
  'members',
];

const includesUsername = (list, username) =>
  Array.isArray(list) && list.includes(username);

// only considers usernames listed explicitly; patterns and CIDRs can also
// blacklist a user, which the server reports via getGroup
export const isBanned = (options, username) =>
  includesUsername(options?.transfers?.groups?.blacklisted?.members, username);

export const getUserDefinedGroups = (options) =>
  Object.entries(options?.transfers?.groups?.userDefined ?? {}).map(
    ([name, group]) => ({ members: group?.members ?? [], name }),
  );

export const getMemberships = (options, username) =>
  getUserDefinedGroups(options)
    .filter((group) => includesUsername(group.members, username))
    .map((group) => group.name);

export const ban = ({ username }) =>
  updateConfiguration((document) =>
    addToList(document, blacklistPath, username),
  );

export const unban = ({ username }) =>
  updateConfiguration((document) =>
    removeFromList(document, blacklistPath, username),
  );

export const addToGroup = ({ group, username }) =>
  updateConfiguration((document) =>
    addToList(document, groupMembersPath(group), username),
  );

export const removeFromGroup = ({ group, username }) =>
  updateConfiguration((document) =>
    // a user defined group with no members is still a group; don't prune it
    removeFromList(document, groupMembersPath(group), username, {
      keepEmpty: true,
    }),
  );
