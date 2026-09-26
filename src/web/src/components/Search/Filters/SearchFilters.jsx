import './SearchFilters.css';
import {
  builtInFilterPresets,
  countFilters,
  deleteFilterPreset,
  getDefaultFilter,
  getFilterPresets,
  parseDuration,
  parseFiltersFromString,
  saveFilterPreset,
  serializeFilters,
  setDefaultFilter as storeDefaultFilter,
} from '../../../lib/searches';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Checkbox,
  Dropdown,
  Form,
  Icon,
  Input,
  Popup,
  Segment,
} from 'semantic-ui-react';

const openKey = 'slskd-search-filters-open';

const invalid = Symbol('invalid');

const formats = [
  'flac',
  'mp3',
  'm4a',
  'aac',
  'ogg',
  'opus',
  'wav',
  'aiff',
  'ape',
  'wv',
  'dsf',
];

const choice = (value, text) => ({ key: String(value), text, value });

const typeOptions = [
  choice('any', 'Any'),
  choice('lossless', 'Lossless'),
  choice('lossy', 'Lossy'),
];

const modeOptions = [
  choice('any', 'Any'),
  choice('cbr', 'CBR'),
  choice('vbr', 'VBR'),
];

const bitRateOptions = [
  choice(0, 'Any'),
  ...[128, 192, 256, 320].map((rate) => choice(rate, `${rate} kbps`)),
];

const bitDepthOptions = [
  choice(0, 'Any'),
  choice(16, '16 bit'),
  choice(24, '24 bit'),
];

const sampleRateOptions = [
  choice(0, 'Any'),
  ...[44_100, 48_000, 88_200, 96_000, 176_400, 192_000].map((rate) =>
    choice(rate, `${rate / 1_000} kHz`),
  ),
];

const help = (
  <div className="search-filters-help">
    <p>Type filters in the box, or use the form. Both edit the same filter.</p>
    <ul>
      <li>
        <code>live -remix</code> filenames must contain every word, and none of
        the words starting with <code>-</code>
      </li>
      <li>
        <code>islossless</code> <code>islossy</code> <code>iscbr</code>{' '}
        <code>isvbr</code>
      </li>
      <li>
        <code>ext:flac,mp3</code> <code>minbr:320</code> <code>minbd:24</code>{' '}
        <code>minsr:96</code>
      </li>
      <li>
        <code>minfs:10mb</code> <code>maxfs:1gb</code> <code>minlen:2:30</code>{' '}
        <code>maxlen:600</code>
      </li>
      <li>
        <code>minfif:8</code> files per folder, <code>maxq:10</code> queue
        length, <code>minspeed:1mb</code> upload speed per second
      </li>
    </ul>
  </div>
);

// keeps what was typed while it is still being typed; "1." or "3:" would otherwise be
// rewritten by the round trip through the filter text
const DraftInput = ({ committed, format, onCommit, parse, ...rest }) => {
  const [draft, setDraft] = useState(() => format(committed));

  useEffect(() => {
    setDraft((current) => {
      const parsed = parse(current);
      return parsed !== invalid &&
        JSON.stringify(parsed) === JSON.stringify(committed)
        ? current
        : format(committed);
    });
  }, [committed, format, parse]);

  return (
    <Form.Input
      {...rest}
      onChange={(_event, { value }) => {
        setDraft(value);
        const parsed = parse(value);
        if (parsed !== invalid) onCommit(parsed);
      }}
      value={draft}
    />
  );
};

const words = {
  format: (list = []) => list.join(' '),
  parse: (text) =>
    text
      .toLowerCase()
      .split(/\s+/u)
      .filter(Boolean)
      .map((word) => word.replace(/^-+/u, ''))
      .filter((word) => word && !word.includes(':')),
};

// numbers shown in a friendlier unit than they're stored in; empty means off
const scaled = (scale, empty) => ({
  format: (value) =>
    value === undefined || value === null || (empty === 0 && !value)
      ? ''
      : String(Number((value / scale).toFixed(2))),
  parse: (text) => {
    if (text.trim() === '') return empty;
    const number = Number(text);
    return Number.isNaN(number) || number < 0
      ? invalid
      : Math.round(number * scale);
  },
});

const units = {
  count: scaled(1, 0),
  maxCount: scaled(1, undefined),
  maxMegabytes: scaled(1_024 ** 2, undefined),
  megabytes: scaled(1_024 ** 2, 0),
  speed: scaled(1_024, 0),
};

const duration = (empty) => ({
  format: (seconds) => {
    if (seconds === undefined || seconds === null || (empty === 0 && !seconds))
      return '';
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  },
  parse: (text) => {
    if (text.trim() === '') return empty;
    return parseDuration(text) ?? invalid;
  },
});

const minDuration = duration(0);
const maxDuration = duration(undefined);

const SearchFilters = ({ onChange, value }) => {
  const [open, setOpen] = useState(
    () => localStorage.getItem(openKey) === 'true',
  );
  const [presets, setPresets] = useState(getFilterPresets);
  const [presetName, setPresetName] = useState('');
  const [defaultFilter, setDefaultFilter] = useState(getDefaultFilter);

  const filters = useMemo(() => parseFiltersFromString(value), [value]);
  const count = countFilters(filters);

  const toggle = () => {
    localStorage.setItem(openKey, String(!open));
    setOpen(!open);
  };

  const update = (changes) =>
    onChange(serializeFilters({ ...filters, ...changes }));

  const field = (key, unit, props) => (
    <DraftInput
      committed={filters[key]}
      format={unit.format}
      inputMode="decimal"
      onCommit={(next) => update({ [key]: next })}
      parse={unit.parse}
      {...props}
    />
  );

  const select = (label, options, current, onSelect) => (
    <Form.Dropdown
      fluid
      label={label}
      onChange={(_event, { value: selected }) => onSelect(selected)}
      options={options}
      selection
      value={current}
    />
  );

  const savePreset = () => {
    const name = presetName.trim();
    if (!name || !value) return;
    setPresets(saveFilterPreset({ filters: value, name }));
    setPresetName('');
  };

  const isDefault = Boolean(value) && value === defaultFilter;

  const presetItem = (preset) => (
    <Dropdown.Item
      active={preset.filters === value}
      key={`${preset.builtIn ? 'builtin' : 'saved'}-${preset.name}`}
      onClick={() => onChange(preset.filters)}
    >
      {!preset.builtIn && (
        <Icon
          className="search-filters-preset-delete"
          link
          name="trash alternate outline"
          onClick={(event) => {
            event.stopPropagation();
            setPresets(deleteFilterPreset(preset.name));
          }}
          title="Delete preset"
        />
      )}
      <span className="search-filters-preset-name-text">{preset.name}</span>
      <span className="search-filters-preset-filters">{preset.filters}</span>
    </Dropdown.Item>
  );

  return (
    <div className="search-filters">
      <div className="search-filters-bar">
        <Button
          active={open}
          className="search-filters-toggle"
          onClick={toggle}
          type="button"
        >
          <Icon name="filter" />
          Filters
          {count > 0 && <span className="search-filters-count">{count}</span>}
          <Icon
            className="search-filters-caret"
            name={open ? 'caret up' : 'caret down'}
          />
        </Button>
        <Input
          action={
            value
              ? {
                  color: 'red',
                  icon: 'x',
                  onClick: () => onChange(''),
                  title: 'Clear filters',
                }
              : undefined
          }
          className="search-filter"
          onChange={(_event, data) => onChange(data.value)}
          placeholder="live -remix islossless minbr:320 ext:flac minfif:8"
          value={value}
        />
        <Dropdown
          button
          className="search-filters-presets icon"
          floating
          icon="bookmark outline"
          labeled
          text="Presets"
        >
          <Dropdown.Menu>
            <Dropdown.Header content="Built in" />
            {builtInFilterPresets.map(presetItem)}
            {presets.length > 0 && <Dropdown.Header content="Saved" />}
            {presets.map(presetItem)}
          </Dropdown.Menu>
        </Dropdown>
        <Popup
          content={help}
          on="click"
          position="bottom right"
          trigger={
            <Button
              className="search-filters-help-button"
              icon="question"
              title="Filter syntax"
              type="button"
            />
          }
          wide="very"
        />
      </div>
      {open && (
        <Segment className="search-filters-panel">
          <Form size="small">
            <div className="search-filters-section">Quality</div>
            <div className="search-filters-grid">
              <Form.Dropdown
                allowAdditions
                fluid
                label="Formats"
                multiple
                onAddItem={() => {}}
                onChange={(_event, { value: selected }) =>
                  update({
                    extensions: selected
                      .map((extension) =>
                        extension.toLowerCase().replace(/^\./u, '').trim(),
                      )
                      .filter(
                        (extension) =>
                          extension && /^[\d_a-z]+$/u.test(extension),
                      ),
                  })
                }
                options={[...new Set([...formats, ...filters.extensions])].map(
                  (extension) => choice(extension, extension.toUpperCase()),
                )}
                placeholder="Any"
                search
                selection
                value={filters.extensions}
              />
              {select(
                'Type',
                typeOptions,
                filters.isLossless
                  ? 'lossless'
                  : filters.isLossy
                    ? 'lossy'
                    : 'any',
                (selected) =>
                  update({
                    isLossless: selected === 'lossless',
                    isLossy: selected === 'lossy',
                  }),
              )}
              {select(
                'Bitrate mode',
                modeOptions,
                filters.isCBR ? 'cbr' : filters.isVBR ? 'vbr' : 'any',
                (selected) =>
                  update({
                    isCBR: selected === 'cbr',
                    isVBR: selected === 'vbr',
                  }),
              )}
              {select(
                'Min bitrate',
                bitRateOptions.some(
                  (option) => option.value === filters.minBitRate,
                )
                  ? bitRateOptions
                  : [
                      ...bitRateOptions,
                      choice(filters.minBitRate, `${filters.minBitRate} kbps`),
                    ],
                filters.minBitRate,
                (selected) => update({ minBitRate: selected }),
              )}
              {select(
                'Min bit depth',
                bitDepthOptions.some(
                  (option) => option.value === filters.minBitDepth,
                )
                  ? bitDepthOptions
                  : [
                      ...bitDepthOptions,
                      choice(filters.minBitDepth, `${filters.minBitDepth} bit`),
                    ],
                filters.minBitDepth,
                (selected) => update({ minBitDepth: selected }),
              )}
              {select(
                'Min sample rate',
                sampleRateOptions.some(
                  (option) => option.value === filters.minSampleRate,
                )
                  ? sampleRateOptions
                  : [
                      ...sampleRateOptions,
                      choice(
                        filters.minSampleRate,
                        `${filters.minSampleRate / 1_000} kHz`,
                      ),
                    ],
                filters.minSampleRate,
                (selected) => update({ minSampleRate: selected }),
              )}
            </div>
            <div className="search-filters-section">Files</div>
            <div className="search-filters-grid">
              {field('minFileSize', units.megabytes, {
                label: 'Min size (MB)',
                placeholder: 'Any',
              })}
              {field('maxFileSize', units.maxMegabytes, {
                label: 'Max size (MB)',
                placeholder: 'Any',
              })}
              {field('minLength', minDuration, {
                label: 'Min length (m:ss)',
                placeholder: 'Any',
              })}
              {field('maxLength', maxDuration, {
                label: 'Max length (m:ss)',
                placeholder: 'Any',
              })}
              {field('minFilesInFolder', units.count, {
                label: 'Min files per folder',
                placeholder: 'Any',
              })}
            </div>
            <div className="search-filters-section">Users</div>
            <div className="search-filters-grid">
              {field('maxQueueLength', units.maxCount, {
                label: 'Max queue length',
                placeholder: 'Any',
              })}
              {field('minUploadSpeed', units.speed, {
                label: 'Min upload speed (KB/s)',
                placeholder: 'Any',
              })}
            </div>
            <div className="search-filters-section">Words in the file path</div>
            <div className="search-filters-grid search-filters-grid-wide">
              {field('include', words, {
                inputMode: 'text',
                label: 'Must include all of',
                placeholder: 'live 2019',
              })}
              {field('exclude', words, {
                inputMode: 'text',
                label: 'Must not include any of',
                placeholder: 'remix karaoke',
              })}
            </div>
          </Form>
          <div className="search-filters-footer">
            <Input
              action={{
                content: 'Save preset',
                disabled: !value || !presetName.trim(),
                icon: 'save',
                onClick: savePreset,
              }}
              className="search-filters-preset-name"
              onChange={(_event, { value: name }) => setPresetName(name)}
              onKeyDown={(event) => event.key === 'Enter' && savePreset()}
              placeholder="Preset name"
              size="small"
              value={presetName}
            />
            <Checkbox
              checked={isDefault}
              disabled={!value && !defaultFilter}
              label={
                defaultFilter && !isDefault
                  ? `Use for new searches (currently "${defaultFilter}")`
                  : 'Use for new searches'
              }
              onChange={(_event, { checked }) => {
                const next = checked ? value : '';
                storeDefaultFilter(next);
                setDefaultFilter(next);
              }}
              toggle
            />
            <Button
              content="Clear all"
              disabled={!value}
              icon="eraser"
              onClick={() => onChange('')}
              size="small"
              type="button"
            />
          </div>
        </Segment>
      )}
    </div>
  );
};

export default SearchFilters;
