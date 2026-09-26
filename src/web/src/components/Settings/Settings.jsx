import './Settings.css';
import '../UserPanel/UserPanel.css';
import { urlBase } from '../../config';
import {
  deleteIn,
  getIn,
  readConfiguration,
  setIn,
  updateConfiguration,
} from '../../lib/configuration';
import { getSchema } from '../../lib/options';
import * as profile from '../../lib/profile';
import AppContext from '../AppContext';
import { getErrorMessage } from '../Users/UserProfile';
import DictionaryEditor from './DictionaryEditor';
import { buildCategories, entryFields } from './generated';
import ProfileEditor from './ProfileEditor';
import sections, {
  fieldPath,
  reservedGroupNames,
  userDefinedGroupsPath,
} from './schema';
import SettingsField from './SettingsField';
import UserGroupsEditor from './UserGroupsEditor';
import React, { useContext, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  Link,
  Prompt,
  Redirect,
  useHistory,
  useParams,
} from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Icon, Input, Menu, Message, Segment } from 'semantic-ui-react';

const descriptionPath = ['soulseek', 'description'];
const picturePath = ['soulseek', 'picture'];

// group names can't contain a NUL, so it's a safe separator
const pathKey = (segments) => segments.join('\u0000');

// whether key is the path itself or a path beneath it
const isWithin = (key, prefix) =>
  key === prefix || key.startsWith(`${prefix}\u0000`);

const allSection = {
  description:
    'Every option in the config file, generated from the application itself. The sections above cover the common ones with friendlier names.',
  icon: 'list',
  key: 'all',
  title: 'All Settings',
};

const navSections = [...sections, allSection];

const withKeys = (field) => ({ ...field, showKey: true });

const getOption = (options, segments) =>
  segments.reduce((node, key) => node?.[key], options);

const sameValue = (a, b, { caseInsensitive = false } = {}) => {
  if (Array.isArray(a) || Array.isArray(b)) {
    return JSON.stringify(a ?? []) === JSON.stringify(b ?? []);
  }

  if (a == null || b == null) {
    return (a ?? '') === (b ?? '');
  }

  return caseInsensitive
    ? String(a).toLowerCase() === String(b).toLowerCase()
    : String(a) === String(b);
};

const validateNumber = (field, raw) => {
  const value = Number(raw);

  if (!Number.isInteger(value)) {
    return 'Enter a whole number.';
  }

  if (field.min != null && value < field.min) {
    return `Must be at least ${field.min.toLocaleString()}.`;
  }

  if (field.max != null && value > field.max) {
    return `Must be at most ${field.max.toLocaleString()}.`;
  }

  return undefined;
};

// turns an edit into a pending change, or undefined when the edit puts the
// value back to what's already in effect
const toChange = (field, raw, effective) => {
  const segments = fieldPath(field);
  const base = { field, segments };

  switch (field.type) {
    case 'password':
      return raw === '' ? undefined : { ...base, value: raw };
    case 'number': {
      if (raw === '') {
        // empty means 'use the default', which for these is unlimited/none
        return { ...base, raw, remove: true };
      }

      const error = validateNumber(field, raw);
      if (error) {
        return { ...base, error, raw };
      }

      const value = Number(raw);
      return sameValue(value, effective) ? undefined : { ...base, raw, value };
    }

    case 'toggle':
      return raw === Boolean(effective) ? undefined : { ...base, value: raw };
    case 'select':
      if (raw === '') {
        return effective == null ? undefined : { ...base, raw, remove: true };
      }

      return sameValue(raw, effective, { caseInsensitive: true })
        ? undefined
        : { ...base, value: raw };
    case 'list':
    case 'multiselect':
    case 'objectList':
      return sameValue(raw, effective) ? undefined : { ...base, value: raw };
    default:
      if (sameValue(raw, effective)) {
        return undefined;
      }

      return raw === '' && field.type !== 'textarea'
        ? { ...base, raw, remove: true }
        : { ...base, value: raw };
  }
};

const matchesQuery = (field, query) =>
  [field.label, field.help, field.key, fieldPath(field).join(' ')]
    .filter(Boolean)
    .some((text) => text.toLowerCase().includes(query));

// a graphical editor for the configuration file, organized like nicotine+'s
// preferences.  edits are collected and written in one go on save, touching
// only the keys that changed
const Settings = () => {
  const { section: sectionKey } = useParams();
  const history = useHistory();
  const { options = {}, state = {} } = useContext(AppContext) ?? {};
  const selfUsername = state.user?.username;
  const canConfigure = Boolean(options.remoteConfiguration);

  const [changes, setChanges] = useState({});
  // saved changes stay on screen until the server reloads the file and
  // pushes new options, so values don't flicker back to their old state
  const [saved, setSaved] = useState({});
  const [picture, setPicture] = useState();
  // names of dictionary entries (user groups, webhooks, ...) added but not
  // saved yet, keyed by the dictionary's path
  const [newEntries, setNewEntries] = useState({});
  const [schemaRoot, setSchemaRoot] = useState();
  const [schemaError, setSchemaError] = useState();
  const [openCategories, setOpenCategories] = useState(() => new Set());
  const [currentPictureUrl, setCurrentPictureUrl] = useState();
  const [fileDocument, setFileDocument] = useState();
  const [loadError, setLoadError] = useState();
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState();
  const [query, setQuery] = useState('');

  const loadFile = async () => {
    try {
      setFileDocument(await readConfiguration());
      setLoadError(undefined);
    } catch (error) {
      const status = error?.response?.status;
      setLoadError(
        status === 401 || status === 403
          ? 'Your account isn’t allowed to change settings. Log in as an administrator.'
          : `Couldn’t read the config file: ${getErrorMessage(error)}`,
      );
    }
  };

  useEffect(() => {
    if (canConfigure) {
      loadFile();
    }
  }, [canConfigure]);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const root = await getSchema();

        if (!cancelled) {
          setSchemaRoot(root);
        }
      } catch (error) {
        if (!cancelled) {
          setSchemaError(getErrorMessage(error));
        }
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  const categories = useMemo(
    () => (schemaRoot ? buildCategories(schemaRoot) : []),
    [schemaRoot],
  );

  const newEntryCount = Object.values(newEntries).reduce(
    (total, names) => total + names.length,
    0,
  );

  useEffect(() => {
    setSaved({});
  }, [options]);

  const configuredPicture = options.soulseek?.picture;

  useEffect(() => {
    let url;
    let cancelled = false;

    const load = async () => {
      try {
        url = await profile.getPictureUrl();

        if (cancelled && url) {
          URL.revokeObjectURL(url);
        } else if (!cancelled) {
          setCurrentPictureUrl(url);
        }
      } catch {
        setCurrentPictureUrl(undefined);
      }
    };

    if (configuredPicture) {
      load();
    } else {
      setCurrentPictureUrl(undefined);
    }

    return () => {
      cancelled = true;

      if (url) {
        URL.revokeObjectURL(url);
      }
    };
  }, [configuredPicture]);

  const dirtyCount =
    Object.keys(changes).length + (picture ? 1 : 0) + newEntryCount;
  const hasErrors = Object.values(changes).some((change) => change.error);

  useEffect(() => {
    if (dirtyCount === 0) {
      return undefined;
    }

    const warn = (event) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirtyCount]);

  const setPictureChange = (next) => {
    if (picture?.url) {
      URL.revokeObjectURL(picture.url);
    }

    setPicture(next);
  };

  const setField = (field, raw) => {
    const segments = fieldPath(field);
    const key = pathKey(segments);
    let change = toChange(field, raw, getOption(options, segments));

    // clearing an unlimited field that the file doesn't set changes nothing,
    // and it already displays empty, so don't count it as a change
    if (
      change?.remove &&
      field.unlimited &&
      fileDocument &&
      getIn(fileDocument, segments) === undefined
    ) {
      change = undefined;
    }

    setChanges((previous) => {
      const next = { ...previous };

      if (change) {
        next[key] = change;
      } else {
        delete next[key];
      }

      return next;
    });
  };

  const resetField = (field) => {
    const segments = fieldPath(field);
    setChanges((previous) => ({
      ...previous,
      [pathKey(segments)]: {
        field,
        raw: '',
        remove: true,
        reset: true,
        segments,
      },
    }));
  };

  const discard = () => {
    setChanges({});
    setPictureChange(undefined);
    setNewEntries({});
    setSaveError(undefined);
  };

  const save = async () => {
    setSaving(true);
    setSaveError(undefined);

    try {
      const pending = Object.values(changes);
      const savedPicturePath = picture?.file
        ? await profile.uploadPicture({ file: picture.file })
        : undefined;

      const document = await updateConfiguration((file) => {
        // removals first; removing a whole group mustn't undo edits elsewhere
        for (const change of pending.filter((entry) => entry.remove)) {
          deleteIn(file, change.segments);
        }

        for (const change of pending.filter((entry) => !entry.remove)) {
          let value = change.value;

          if (change.field.type === 'list') {
            value = value.map((item) => item.trim()).filter(Boolean);
          } else if (change.field.type === 'objectList') {
            value = value.filter((row) =>
              Object.values(row ?? {}).some((cell) => String(cell).trim()),
            );
          }

          setIn(file, change.segments, value);
        }

        for (const [dictionaryKey, names] of Object.entries(newEntries)) {
          const dictionaryPath = dictionaryKey.split('\u0000');
          const isUserGroup = dictionaryKey === pathKey(userDefinedGroupsPath);

          for (const name of names) {
            const entryPath = [...dictionaryPath, name];

            if (getIn(file, entryPath) == null) {
              if (isUserGroup) {
                setIn(file, [...entryPath, 'members'], []);
              } else {
                setIn(file, entryPath, {});
              }
            }
          }
        }

        if (savedPicturePath) {
          setIn(file, picturePath, savedPicturePath);
        } else if (picture?.remove) {
          deleteIn(file, picturePath);
        }
      });

      const needsRestart = pending.some((change) => change.field?.restart);
      const needsReconnect = pending.some((change) => change.field?.reconnect);

      setSaved(changes);
      setChanges({});
      setNewEntries({});
      setPictureChange(undefined);
      setFileDocument(document);

      if (needsRestart) {
        toast.success(
          'Settings saved. Some changes take effect after a restart.',
        );
      } else if (needsReconnect) {
        toast.success(
          'Settings saved. Some changes take effect after reconnecting.',
        );
      } else {
        toast.success('Settings saved.');
      }
    } catch (error) {
      setSaveError(getErrorMessage(error));
    } finally {
      setSaving(false);
    }
  };

  const renderField = (field) => {
    const segments = fieldPath(field);
    const key = pathKey(segments);
    const effective = getOption(options, segments);
    const change = changes[key];
    const savedChange = saved[key];
    const pending = change ?? savedChange;

    let value = effective;
    if (pending && !pending.reset) {
      value = pending.raw ?? pending.value;
    }

    const fileValue = fileDocument ? getIn(fileDocument, segments) : undefined;
    const overridden =
      !pending &&
      fileValue != null &&
      effective !== undefined &&
      !field.secret &&
      !field.isPath &&
      !sameValue(fileValue, effective, {
        caseInsensitive: field.type === 'select',
      });

    let help = field.help;
    if (change?.reset) {
      help = 'Will use the default after saving.';
    }

    return (
      <SettingsField
        changed={Boolean(change)}
        disabled={!canConfigure || Boolean(loadError) || saving}
        error={change?.error}
        field={{ ...field, help }}
        key={field.key}
        onChange={(raw) => setField(field, raw)}
        onReset={
          fileValue !== undefined && !change && canConfigure && !field.secret
            ? () => resetField(field)
            : undefined
        }
        overriddenBy={overridden ? fileValue : undefined}
        pendingSecret={field.secret ? change?.value : undefined}
        value={value}
      />
    );
  };

  // the entries of a dictionary option: what's in effect, plus unsaved additions
  const entriesOf = (dictionaryPath) => {
    const added = newEntries[pathKey(dictionaryPath)] ?? [];
    const existing = Object.keys(getOption(options, dictionaryPath) ?? {}).map(
      (name) => ({
        name,
        removed: Boolean(changes[pathKey([...dictionaryPath, name])]?.remove),
      }),
    );

    return [
      ...existing,
      ...added
        .filter((name) => !existing.some((entry) => entry.name === name))
        .map((name) => ({ isNew: true, name, removed: false })),
    ];
  };

  const addEntry = (dictionaryPath, name) =>
    setNewEntries((previous) => {
      const key = pathKey(dictionaryPath);
      return { ...previous, [key]: [...(previous[key] ?? []), name] };
    });

  const removeEntry = (dictionaryPath, name) => {
    const entryPath = [...dictionaryPath, name];
    const prefix = pathKey(entryPath);
    const dictionaryKey = pathKey(dictionaryPath);
    const isNew = (newEntries[dictionaryKey] ?? []).includes(name);

    setChanges((previous) => {
      const next = Object.fromEntries(
        Object.entries(previous).filter(([key]) => !isWithin(key, prefix)),
      );

      if (!isNew) {
        next[prefix] = {
          field: { label: name },
          remove: true,
          segments: entryPath,
        };
      }

      return next;
    });
    setNewEntries((previous) => ({
      ...previous,
      [dictionaryKey]: (previous[dictionaryKey] ?? []).filter(
        (entry) => entry !== name,
      ),
    }));
  };

  const restoreEntry = (dictionaryPath, name) =>
    setChanges((previous) => {
      const next = { ...previous };
      delete next[pathKey([...dictionaryPath, name])];
      return next;
    });

  const section = navSections.find((candidate) => candidate.key === sectionKey);

  if (!section) {
    return <Redirect to={`${urlBase}/settings/${sections[0].key}`} />;
  }

  const normalizedQuery = query.trim().toLowerCase();

  const sectionChangeCount = (candidate) => {
    const keys = new Set(
      (candidate.groups ?? []).flatMap((group) =>
        group.fields.map((field) => pathKey(fieldPath(field))),
      ),
    );

    let count = Object.keys(changes).filter((key) => keys.has(key)).length;

    if (candidate.key === 'profile') {
      count += (changes[pathKey(descriptionPath)] ? 1 : 0) + (picture ? 1 : 0);
    }

    if (candidate.userGroups) {
      const groupPrefix = pathKey(userDefinedGroupsPath);
      count +=
        Object.keys(changes).filter((key) => isWithin(key, groupPrefix))
          .length + (newEntries[groupPrefix]?.length ?? 0);
    }

    if (candidate.key === allSection.key) {
      count = Object.keys(changes).length + newEntryCount;
    }

    return count;
  };

  const renderGroups = (groups, { forceOpen = false } = {}) =>
    groups.map((group) =>
      group.advanced && !forceOpen ? (
        <details
          className="settings-group settings-advanced ui segment"
          key={group.key ?? group.title}
        >
          <summary>
            <h3>{group.title}</h3>
            <span className="settings-advanced-hint">Advanced</span>
          </summary>
          {group.help && <p className="settings-group-help">{group.help}</p>}
          {group.fields.map(renderField)}
        </details>
      ) : (
        <Segment
          className="settings-group"
          key={group.key ?? group.title}
        >
          <h3>{group.title}</h3>
          {group.help && <p className="settings-group-help">{group.help}</p>}
          {group.fields.map(renderField)}
        </Segment>
      ),
    );

  const renderSearchResults = () => {
    const results = sections
      .map((candidate) => ({
        groups: (candidate.groups ?? [])
          .map((group) => ({
            ...group,
            advanced: false,
            fields: group.fields.filter((field) =>
              matchesQuery(field, normalizedQuery),
            ),
            title: `${candidate.title} › ${group.title}`,
          }))
          .filter((group) => group.fields.length > 0),
        section: candidate,
      }))
      .filter((result) => result.groups.length > 0);

    const curatedKeys = new Set(
      sections.flatMap((candidate) =>
        (candidate.groups ?? []).flatMap((group) =>
          group.fields.map((field) => pathKey(fieldPath(field))),
        ),
      ),
    );

    const generatedResults = categories.flatMap((category) =>
      category.groups
        .map((group) => ({
          fields: group.fields
            .filter((field) => !curatedKeys.has(pathKey(fieldPath(field))))
            .filter((field) => matchesQuery(field, normalizedQuery))
            .map(withKeys),
          key: `all-${group.key}`,
          title: `All Settings › ${group.title}`,
        }))
        .filter((group) => group.fields.length > 0),
    );

    results.push(...generatedResults.map((group) => ({ groups: [group] })));

    const profileMatches = ['profile', 'description', 'picture', 'avatar'].some(
      (word) => word.includes(normalizedQuery),
    );

    if (results.length === 0 && !profileMatches) {
      return (
        <Segment
          className="settings-group"
          placeholder
        >
          <p>No settings match “{query}”.</p>
        </Segment>
      );
    }

    return (
      <>
        {profileMatches && (
          <Segment className="settings-group">
            <h3>Profile</h3>
            <Link to={`${urlBase}/settings/profile`}>
              Edit your description and picture
            </Link>
          </Segment>
        )}
        {results.flatMap((result) =>
          renderGroups(result.groups, { forceOpen: true }),
        )}
      </>
    );
  };

  const renderDictionary = (dictionary) => {
    const isUserGroups =
      pathKey(dictionary.segments) === pathKey(userDefinedGroupsPath);

    return (
      <div
        className="settings-dictionary"
        key={dictionary.key}
      >
        <h4>{dictionary.title}</h4>
        {dictionary.help && (
          <p className="settings-group-help">{dictionary.help}</p>
        )}
        <DictionaryEditor
          disabled={!canConfigure || Boolean(loadError) || saving}
          entries={entriesOf(dictionary.segments)}
          fieldsFor={(name) =>
            entryFields(dictionary.itemSchema, [
              ...dictionary.segments,
              name,
            ]).map(withKeys)
          }
          noun={isUserGroups ? 'group' : 'entry'}
          onAdd={(name) => addEntry(dictionary.segments, name)}
          onRemove={(name) => removeEntry(dictionary.segments, name)}
          onRestore={(name) => restoreEntry(dictionary.segments, name)}
          renderField={renderField}
          reservedNames={isUserGroups ? reservedGroupNames : []}
        />
      </div>
    );
  };

  const categoryChangeCount = (category) => {
    const prefixes = category.groups.flatMap((group) => [
      ...group.fields.map((field) => pathKey(fieldPath(field))),
      ...(group.dictionaries ?? []).map((dictionary) =>
        pathKey(dictionary.segments),
      ),
    ]);

    return (
      Object.keys(changes).filter((key) =>
        prefixes.some((prefix) => isWithin(key, prefix)),
      ).length +
      category.groups
        .flatMap((group) => group.dictionaries ?? [])
        .reduce(
          (total, dictionary) =>
            total + (newEntries[pathKey(dictionary.segments)]?.length ?? 0),
          0,
        )
    );
  };

  const toggleCategory = (key, open) =>
    setOpenCategories((previous) => {
      const next = new Set(previous);

      if (open) {
        next.add(key);
      } else {
        next.delete(key);
      }

      return next;
    });

  const renderAllSettings = () => {
    if (schemaError) {
      return (
        <Message negative>
          <Message.Header>Couldn’t load the list of settings</Message.Header>
          <p>{schemaError}</p>
        </Message>
      );
    }

    if (!schemaRoot) {
      return (
        <Segment
          className="settings-group"
          loading
          placeholder
        />
      );
    }

    const allOpen = categories.every((category) =>
      openCategories.has(category.key),
    );

    return (
      <>
        <div className="settings-all-toolbar">
          <Button
            content={allOpen ? 'Collapse all' : 'Expand all'}
            icon={allOpen ? 'compress' : 'expand'}
            onClick={() =>
              setOpenCategories(
                allOpen
                  ? new Set()
                  : new Set(categories.map((category) => category.key)),
              )
            }
            size="small"
            type="button"
          />
        </div>
        {categories.map((category) => {
          const count = category.groups.reduce(
            (total, group) => total + group.fields.length,
            0,
          );
          const changed = categoryChangeCount(category);
          const open = openCategories.has(category.key);

          return (
            <details
              className="settings-group settings-advanced settings-category ui segment"
              key={category.key}
              onToggle={(event) =>
                toggleCategory(category.key, event.currentTarget.open)
              }
              open={open}
            >
              <summary>
                <h3>{category.title}</h3>
                <span className="settings-advanced-hint">
                  {count} setting{count === 1 ? '' : 's'}
                </span>
                {changed > 0 && (
                  <span
                    aria-label={`${changed} unsaved`}
                    className="settings-changed-dot"
                    title={`${changed} unsaved`}
                  />
                )}
              </summary>
              {open &&
                category.groups.map((group) => (
                  <div
                    className="settings-subgroup"
                    key={group.key}
                  >
                    {group.title !== category.title && <h4>{group.title}</h4>}
                    {group.fields.map((field) => renderField(withKeys(field)))}
                    {(group.dictionaries ?? []).map(renderDictionary)}
                  </div>
                ))}
            </details>
          );
        })}
      </>
    );
  };

  const renderSection = () => {
    if (section.key === allSection.key) {
      return renderAllSettings();
    }

    if (section.key === 'profile') {
      const change = changes[pathKey(descriptionPath)];
      const pending = change ?? saved[pathKey(descriptionPath)];

      return (
        <ProfileEditor
          currentPictureUrl={currentPictureUrl}
          description={
            pending ? pending.value : getOption(options, descriptionPath)
          }
          descriptionChanged={Boolean(change)}
          disabled={!canConfigure || Boolean(loadError) || saving}
          onDescriptionChange={(value) =>
            setField(
              {
                key: 'soulseek.description',
                label: 'Description',
                type: 'textarea',
              },
              value,
            )
          }
          onPictureChange={setPictureChange}
          picture={picture}
          username={selfUsername}
        />
      );
    }

    return (
      <>
        {renderGroups(section.groups ?? [])}
        {section.userGroups && (
          <UserGroupsEditor
            disabled={!canConfigure || Boolean(loadError) || saving}
            groups={entriesOf(userDefinedGroupsPath)}
            onAdd={(name) => addEntry(userDefinedGroupsPath, name)}
            onRemove={(name) => removeEntry(userDefinedGroupsPath, name)}
            onRestore={(name) => restoreEntry(userDefinedGroupsPath, name)}
            renderField={renderField}
          />
        )}
      </>
    );
  };

  return (
    <div className="settings">
      <Prompt
        message={(location) =>
          location.pathname.startsWith(`${urlBase}/settings`) ||
          'You have unsaved settings. Leave without saving them?'
        }
        when={dirtyCount > 0}
      />
      <Segment
        className="settings-header"
        raised
      >
        <Icon
          name="sliders horizontal"
          size="big"
        />
        <h1>Settings</h1>
        <Input
          aria-label="Find a setting"
          className="settings-search"
          icon="search"
          onChange={(_event, { value }) => setQuery(value)}
          placeholder="Find a setting"
          type="search"
          value={query}
        />
      </Segment>
      {!canConfigure && (
        <Message warning>
          <Message.Header>Settings are read-only</Message.Header>
          <p>
            Changing settings from the web UI is turned off. To turn it on, set{' '}
            <code>remote_configuration: true</code> in the config file (or{' '}
            <code>SLSKD_REMOTE_CONFIGURATION=true</code>) and restart.
          </p>
        </Message>
      )}
      {canConfigure && loadError && (
        <Message negative>
          <Message.Header>Settings can’t be changed</Message.Header>
          <p>{loadError}</p>
        </Message>
      )}
      <div className="settings-layout">
        <nav
          aria-label="Settings sections"
          className="settings-nav"
        >
          <Menu
            fluid
            secondary
            vertical
          >
            {navSections.map((candidate) => {
              const count = sectionChangeCount(candidate);

              return (
                <Menu.Item
                  active={!normalizedQuery && candidate.key === section.key}
                  key={candidate.key}
                  onClick={() => {
                    setQuery('');
                    history.push(`${urlBase}/settings/${candidate.key}`);
                  }}
                >
                  <Icon name={candidate.icon} />
                  {candidate.title}
                  {count > 0 && (
                    <span
                      aria-label={`${count} unsaved`}
                      className="settings-changed-dot"
                      title={`${count} unsaved`}
                    />
                  )}
                </Menu.Item>
              );
            })}
            <Menu.Item
              as={Link}
              className="settings-nav-yaml"
              to={`${urlBase}/system/options`}
            >
              <Icon name="file code outline" />
              Config File (YAML)
            </Menu.Item>
          </Menu>
        </nav>
        <main className="settings-content">
          {normalizedQuery ? (
            <>
              <div className="settings-section-header">
                <h2>Settings matching “{query}”</h2>
              </div>
              {renderSearchResults()}
            </>
          ) : (
            <>
              <div className="settings-section-header">
                <h2>{section.title}</h2>
                <p>{section.description}</p>
                {section.link && (
                  <Link to={`${urlBase}${section.link.to}`}>
                    {section.link.text}
                  </Link>
                )}
              </div>
              {renderSection()}
            </>
          )}
        </main>
      </div>
      {/* rendered into body so position: fixed is relative to the viewport; the
          app content area is transformed, which would make it scroll away */}
      {(dirtyCount > 0 || saveError) &&
        createPortal(
          <div
            aria-live="polite"
            className="settings-savebar"
            role="region"
          >
            <div className="settings-savebar-inner">
              {saveError ? (
                <span className="settings-savebar-error">
                  <Icon name="warning circle" />
                  {saveError}
                </span>
              ) : (
                <span>
                  {dirtyCount} unsaved change{dirtyCount === 1 ? '' : 's'}
                  {hasErrors && ' · fix the highlighted fields to save'}
                </span>
              )}
              <div className="settings-savebar-buttons">
                <Button
                  content="Discard"
                  disabled={saving || dirtyCount === 0}
                  onClick={discard}
                />
                <Button
                  content="Save"
                  disabled={saving || hasErrors || dirtyCount === 0}
                  icon="save"
                  loading={saving}
                  onClick={save}
                  primary
                />
              </div>
            </div>
          </div>,
          document.body,
        )}
    </div>
  );
};

export default Settings;
