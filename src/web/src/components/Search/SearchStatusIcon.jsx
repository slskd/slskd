import React from 'react';
import { Icon, Popup } from 'semantic-ui-react';

// as of 3/26/25 states are:
// transient:
//   None, Queued, Requested, InProgress
// terminal:
//   good: Completed, [TimedOut | ResponseLimitReached | FileLimitReached]
//   bad: Completed, [Errored | Cancelled]

const getIcon = ({ state, ...props }) => {
  switch (state) {
    case 'None':
    case 'Queued':
    case 'Requested':
      return (
        <Icon
          name="time"
          {...props}
        />
      );
    case 'InProgress':
      return (
        <Icon
          color="green"
          loading
          name="circle notch"
          {...props}
        />
      );
    case 'Completed, TimedOut':
    case 'Completed, ResponseLimitReached':
    case 'Completed, FileLimitReached':
      return (
        <Icon
          color="green"
          name="check"
          {...props}
        />
      );
    case 'Completed, Cancelled':
      return (
        <Icon
          color="green"
          name="stop circle"
          {...props}
        />
      );
    case 'Completed, Errored':
      return (
        <Icon
          color="red"
          name="x"
          {...props}
        />
      );
    default:
      return (
        <Icon
          color="yellow"
          name="question circle"
          {...props}
        />
      );
  }
};

const SearchStatusIcon = ({ state, ...props }) => (
  <Popup
    content={state}
    trigger={getIcon({ state, ...props })}
  />
);

export default SearchStatusIcon;
