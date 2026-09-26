import React, { useState } from 'react';
import { Button, Input, Label, Message } from 'semantic-ui-react';

// the server lowercases keys and drops underscores and dashes, so an entry
// named 'My_Buddies' is really 'mybuddies'; show people the name that sticks
export const normalizeEntryName = (name) =>
  name
    .trim()
    .toLowerCase()
    .replaceAll(/[_\s-]/gu, '');

// named entries that share a shape, like user groups, webhooks or scripts
const DictionaryEditor = ({
  addLabel = 'Add',
  deleteLabel = 'Delete',
  disabled,
  entries,
  fieldsFor,
  noun = 'entry',
  onAdd,
  onRemove,
  onRestore,
  placeholder,
  renderField,
  reservedNames = [],
}) => {
  const [draft, setDraft] = useState('');
  const name = normalizeEntryName(draft);
  const taken = entries.some((entry) => entry.name === name);
  const reserved = reservedNames.includes(name);
  const invalid = name.length > 0 && !/^[\da-z]+$/u.test(name);

  let problem;
  if (taken) problem = `A ${noun} with that name already exists.`;
  else if (reserved) problem = `“${name}” is a built-in ${noun}.`;
  else if (invalid) problem = 'Use letters and numbers only.';

  const add = () => {
    if (name && !problem) {
      onAdd(name);
      setDraft('');
    }
  };

  return (
    <>
      {entries.map((entry) =>
        entry.removed ? (
          <Message
            className="settings-user-group-removed"
            key={entry.name}
            size="small"
            warning
          >
            The {noun} <strong>{entry.name}</strong> will be deleted when you
            save.{' '}
            <Button
              content="Undo"
              onClick={() => onRestore(entry.name)}
              size="mini"
              type="button"
            />
          </Message>
        ) : (
          <div
            className="settings-user-group"
            key={entry.name}
          >
            <div className="settings-user-group-header">
              <h4>{entry.name}</h4>
              {entry.isNew && (
                <Label
                  color="green"
                  size="mini"
                >
                  new
                </Label>
              )}
              <Button
                className="settings-user-group-delete"
                content={deleteLabel}
                disabled={disabled}
                icon="trash alternate outline"
                onClick={() => onRemove(entry.name)}
                size="mini"
                type="button"
              />
            </div>
            {fieldsFor(entry.name).map((field) => renderField(field))}
          </div>
        ),
      )}
      <div className="settings-user-group-add">
        <Input
          aria-label={`New ${noun} name`}
          disabled={disabled}
          error={Boolean(problem)}
          onChange={(_event, data) => setDraft(data.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add();
            }
          }}
          placeholder={placeholder ?? `New ${noun} name`}
          value={draft}
        />
        <Button
          content={addLabel}
          disabled={disabled || !name || Boolean(problem)}
          icon="plus"
          onClick={add}
          type="button"
        />
      </div>
      {problem && <div className="settings-field-help error">{problem}</div>}
    </>
  );
};

export default DictionaryEditor;
