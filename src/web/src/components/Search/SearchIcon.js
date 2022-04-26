import React from 'react';

import { Icon, Popup } from 'semantic-ui-react';

// as of 4.5.2, states are:
// transient:
//   None, Requested, InProgress
// terminal:
//   good: Completed, [TimedOut | ResponseLimitReached | FileLimitReached]
//   bad: Completed, [Errored | Cancelled]

const getIcon = (state) => {
  switch (state) {
    case 'None':
    case 'Requested':
      return <Icon name='time'/>
    case 'InProgress':
      return <Icon name='spinner' loading color='green'/>
    case 'Completed, TimedOut':
    case 'Completed, ResponseLimitReached':
    case 'Completed, FileLimitReached':
      return <Icon name='check' color='green'/>
    case 'Completed, Cancelled':
      return <Icon name='stop circle' color='green'/>
    case 'Completed, Errored':
      return <Icon name='x' color='red'/>
    default:
      return <Icon name='question circle' color='yellow'/>
  }
}

const SearchIcon = ({ state, ...props }) => 
  <Popup content={state} trigger={getIcon(state)} {...props}/>

export default SearchIcon;