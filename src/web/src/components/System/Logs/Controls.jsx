import './Logs.css';
import React from 'react';
import { Dropdown, Input, Segment } from 'semantic-ui-react';

/**
 * Serilog's levels, ordered least to most severe.  Selecting a level displays
 * that level and everything below it in this list.
 */
export const levels = [
  'Verbose',
  'Debug',
  'Information',
  'Warning',
  'Error',
  'Fatal',
];

const levelOptions = levels.map((level, index) => ({
  key: level,
  text: index === 0 ? `${level} (all)` : `${level} and above`,
  value: level,
}));

export const perPageOptions = [50, 100, 200, 500, 1_000].map((count) => ({
  key: count,
  text: `${count} per page`,
  value: count,
}));

export const defaultPerPage = 200;

const Controls = ({
  filter,
  level,
  onFilterChange,
  onLevelChange,
  onPerPageChange,
  perPage,
}) => {
  return (
    <Segment
      className="logs-options"
      raised
    >
      <Dropdown
        button
        className="logs-options-level icon"
        floating
        icon="bars"
        labeled
        onChange={(_event, { value }) => onLevelChange(value)}
        options={levelOptions}
        text={levelOptions.find((o) => o.value === level)?.text}
      />
      <Dropdown
        button
        className="logs-options-per-page icon"
        floating
        icon="list ol"
        labeled
        onChange={(_event, { value }) => onPerPageChange(value)}
        options={perPageOptions}
        text={perPageOptions.find((o) => o.value === perPage)?.text}
      />
      <Input
        action={
          Boolean(filter) && {
            color: 'red',
            icon: 'x',
            onClick: () => onFilterChange(''),
          }
        }
        className="logs-filter"
        label={{ content: 'Filter', icon: 'filter' }}
        onChange={(_event, data) => onFilterChange(data.value)}
        placeholder="Show only lines matching this text"
        value={filter}
      />
    </Segment>
  );
};

export default Controls;
