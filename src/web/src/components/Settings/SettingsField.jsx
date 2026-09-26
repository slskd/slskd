import { toYamlKey } from '../../lib/configuration';
import { unlimitedValue } from './schema';
import React, { useMemo, useState } from 'react';
import {
  Button,
  Checkbox,
  Dropdown,
  Icon,
  Input,
  Label,
  Popup,
  TextArea,
} from 'semantic-ui-react';

let nextId = 0;
// eslint-disable-next-line no-plusplus
const useId = () => useMemo(() => `settings-field-${nextId++}`, []);

const ListEditor = ({ disabled, id, onChange, placeholder, value }) => {
  const [draft, setDraft] = useState('');
  const items = Array.isArray(value) ? value : [];

  const add = () => {
    const entry = draft.trim();

    if (entry && !items.includes(entry)) {
      onChange([...items, entry]);
    }

    setDraft('');
  };

  return (
    <div className="settings-list">
      {items.length > 0 && (
        <ul className="settings-list-items">
          {items.map((item, index) => (
            // eslint-disable-next-line react/no-array-index-key
            <li key={`${item}-${index}`}>
              <Input
                aria-label={`Item ${index + 1}`}
                disabled={disabled}
                fluid
                onChange={(_event, data) =>
                  onChange(
                    items.map((existing, position) =>
                      position === index ? data.value : existing,
                    ),
                  )
                }
                value={item}
              />
              <Button
                aria-label={`Remove ${item}`}
                content="Remove"
                disabled={disabled}
                icon="close"
                onClick={() =>
                  onChange(items.filter((_, position) => position !== index))
                }
                type="button"
              />
            </li>
          ))}
        </ul>
      )}
      <div className="settings-list-add">
        <Input
          disabled={disabled}
          fluid
          id={id}
          onChange={(_event, data) => setDraft(data.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add();
            }
          }}
          placeholder={placeholder ?? 'Add an entry'}
          value={draft}
        />
        <Button
          content="Add"
          disabled={disabled || !draft.trim()}
          icon="plus"
          onClick={add}
          type="button"
        />
      </div>
    </div>
  );
};

// rows of small objects, like http headers; each column is a text input
const ObjectListEditor = ({ columns, disabled, id, onChange, value }) => {
  const rows = Array.isArray(value) ? value : [];
  const blank = Object.fromEntries(columns.map((column) => [column.name, '']));

  const update = (index, name, text) =>
    onChange(
      rows.map((row, position) =>
        position === index ? { ...row, [name]: text } : row,
      ),
    );

  return (
    <div className="settings-list">
      {rows.length > 0 && (
        <ul className="settings-list-items">
          {rows.map((row, index) => (
            // eslint-disable-next-line react/no-array-index-key
            <li key={index}>
              {columns.map((column) => (
                <Input
                  aria-label={`${column.label} ${index + 1}`}
                  disabled={disabled}
                  key={column.name}
                  onChange={(_event, data) =>
                    update(index, column.name, data.value)
                  }
                  placeholder={column.label}
                  value={row?.[column.name] ?? ''}
                />
              ))}
              <Button
                aria-label={`Remove row ${index + 1}`}
                content="Remove"
                disabled={disabled}
                icon="close"
                onClick={() =>
                  onChange(rows.filter((_, position) => position !== index))
                }
                type="button"
              />
            </li>
          ))}
        </ul>
      )}
      <Button
        content="Add"
        disabled={disabled}
        icon="plus"
        id={id}
        onClick={() => onChange([...rows, blank])}
        type="button"
      />
    </div>
  );
};

// long keys may only wrap after a dot or an underscore, not mid-word
const breakableKey = (key) =>
  key.split(/(?<=[._])/u).map((part, index) => (
    // eslint-disable-next-line react/no-array-index-key
    <React.Fragment key={index}>
      {index > 0 && <wbr />}
      {part}
    </React.Fragment>
  ));

const matchOption = (options, value) =>
  options.find(
    ([option]) => String(option).toLowerCase() === String(value).toLowerCase(),
  )?.[0] ?? value;

// editors for collections; kept apart from SettingsField to keep it readable
const collectionTypes = new Set(['list', 'multiselect', 'objectList']);

const CollectionControl = ({
  describedBy,
  disabled,
  field,
  id,
  onChange,
  value,
}) => {
  switch (field.type) {
    case 'multiselect':
      return (
        <Dropdown
          aria-describedby={describedBy}
          disabled={disabled}
          fluid
          id={id}
          multiple
          onChange={(_event, data) => onChange(data.value)}
          options={field.options.map(([optionValue, text]) => ({
            key: optionValue,
            text,
            value: optionValue,
          }))}
          search
          selection
          value={(Array.isArray(value) ? value : []).map((item) =>
            matchOption(field.options, item),
          )}
        />
      );
    case 'objectList':
      return (
        <ObjectListEditor
          columns={field.columns}
          disabled={disabled}
          id={id}
          onChange={onChange}
          value={value}
        />
      );
    default:
      return (
        <ListEditor
          disabled={disabled}
          id={id}
          onChange={onChange}
          placeholder={field.placeholder}
          value={value}
        />
      );
  }
};

// renders a single setting.  `value` is what to display (pending change or
// effective value); `onChange` receives the new raw value
const SettingsField = ({
  changed,
  disabled,
  error,
  field,
  onChange,
  onReset,
  overriddenBy,
  pendingSecret,
  value,
}) => {
  const id = useId();
  const helpId = `${id}-help`;
  const {
    empty,
    help,
    label,
    max,
    min,
    options,
    placeholder,
    reconnect,
    restart,
    type,
    unit,
    unlimited,
  } = field;

  const describedBy = help || error ? helpId : undefined;

  const control = () => {
    if (collectionTypes.has(type)) {
      return (
        <CollectionControl
          describedBy={describedBy}
          disabled={disabled}
          field={field}
          id={id}
          onChange={onChange}
          value={value}
        />
      );
    }

    switch (type) {
      case 'toggle':
        return (
          <Checkbox
            aria-describedby={describedBy}
            checked={Boolean(value)}
            disabled={disabled}
            id={id}
            onChange={(_event, data) => onChange(data.checked)}
            toggle
          />
        );
      case 'select':
        return (
          <Dropdown
            aria-describedby={describedBy}
            disabled={disabled}
            id={id}
            onChange={(_event, data) => onChange(data.value)}
            options={options.map(([optionValue, text]) => ({
              key: optionValue,
              text,
              value: optionValue,
            }))}
            selection
            value={matchOption(options, value)}
          />
        );
      case 'textarea':
        return (
          <TextArea
            aria-describedby={describedBy}
            disabled={disabled}
            id={id}
            onChange={(_event, data) => onChange(data.value)}
            rows={6}
            value={value ?? ''}
          />
        );
      case 'password':
        return (
          <Input
            aria-describedby={describedBy}
            autoComplete="new-password"
            disabled={disabled}
            fluid
            id={id}
            onChange={(_event, data) => onChange(data.value)}
            placeholder={pendingSecret ? '' : 'Unchanged'}
            type="password"
            value={pendingSecret ?? ''}
          />
        );
      case 'number': {
        let display = value ?? '';

        if (unlimited && Number(display) === unlimitedValue) {
          display = '';
        }

        return (
          <Input
            aria-describedby={describedBy}
            className="settings-number"
            disabled={disabled}
            error={Boolean(error)}
            id={id}
            inputMode="numeric"
            label={unit ? { basic: true, content: unit } : undefined}
            labelPosition={unit ? 'right' : undefined}
            max={max}
            min={min}
            onChange={(_event, data) => onChange(data.value)}
            placeholder={unlimited ? 'Unlimited' : empty ?? 'Default'}
            type="number"
            value={display}
          />
        );
      }

      default:
        return (
          <Input
            aria-describedby={describedBy}
            disabled={disabled}
            fluid
            id={id}
            onChange={(_event, data) => onChange(data.value)}
            placeholder={placeholder ?? empty}
            value={value ?? ''}
          />
        );
    }
  };

  return (
    <div className={`settings-field settings-field-${type}`}>
      <div className="settings-field-label">
        <label htmlFor={id}>{label}</label>
        {field.showKey && (
          <code className="settings-field-key">
            {breakableKey(field.segments.map(toYamlKey).join('.'))}
          </code>
        )}
        <div className="settings-field-badges">
          {changed && (
            <span
              aria-label="Unsaved change"
              className="settings-changed-dot"
              title="Unsaved change"
            />
          )}
          {restart && (
            <Label
              basic
              size="mini"
              title="Takes effect after the application restarts"
            >
              restart
            </Label>
          )}
          {reconnect && (
            <Label
              basic
              size="mini"
              title="Takes effect after reconnecting to the server"
            >
              reconnect
            </Label>
          )}
          {overriddenBy !== undefined && (
            <Popup
              content={`The config file sets this to ${JSON.stringify(
                overriddenBy,
              )}, but an environment variable or command line argument overrides it. Changes made here won't take effect until that override is removed.`}
              trigger={
                <Label
                  color="orange"
                  size="mini"
                >
                  <Icon name="warning sign" />
                  overridden
                </Label>
              }
              wide
            />
          )}
        </div>
      </div>
      <div className="settings-field-control">
        <div className="settings-field-input">
          {control()}
          {onReset && (
            <Button
              aria-label={`Reset ${label} to the default`}
              className="settings-reset"
              icon="undo"
              onClick={onReset}
              size="mini"
              title="Remove from the config file and use the default"
              type="button"
            />
          )}
        </div>
        {(help || error) && (
          <div
            className={`settings-field-help ${error ? 'error' : ''}`}
            id={helpId}
          >
            {error ?? help}
          </div>
        )}
      </div>
    </div>
  );
};

export default SettingsField;
