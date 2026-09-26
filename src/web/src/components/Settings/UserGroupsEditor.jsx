import DictionaryEditor from './DictionaryEditor';
import { reservedGroupNames, userGroupFields } from './schema';
import React from 'react';
import { Segment } from 'semantic-ui-react';

// user defined groups: named lists of users with their own upload rules,
// like a buddy list with perks
const UserGroupsEditor = ({
  disabled,
  groups,
  onAdd,
  onRemove,
  onRestore,
  renderField,
}) => (
  <Segment className="settings-group">
    <h3>User groups</h3>
    <p className="settings-group-help">
      Give specific users their own upload slots, speed and priority, for
      example friends you want to serve first. You can also add users to a group
      from their profile.
    </p>
    <DictionaryEditor
      addLabel="Add Group"
      deleteLabel="Delete Group"
      disabled={disabled}
      entries={groups}
      fieldsFor={userGroupFields}
      noun="group"
      onAdd={onAdd}
      onRemove={onRemove}
      onRestore={onRestore}
      placeholder="New group name, like buddies"
      renderField={renderField}
      reservedNames={reservedGroupNames}
    />
  </Segment>
);

export default UserGroupsEditor;
