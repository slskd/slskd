import React from 'react';

import { Icon, Popup } from 'semantic-ui-react';

// as of 4.5.2, states are:
// transient:
//   None, Requested, InProgress
// terminal:
//   good: Completed, [TimedOut | ResponseLimitReached | FileLimitReached]
//   bad: Completed, [Errored | Cancelled]

const getIcon = ({ state, ...props }) => {
  switch (state) {
  case 'None':
  case 'Requested':
    return <Icon name='time' {...props}/>
  case 'InProgress':
    return <Icon name='circle notch' loading color='green' {...props}/>
  case 'Completed, TimedOut':
  case 'Completed, ResponseLimitReached':
  case 'Completed, FileLimitReached':
    return <Icon name='check' color='green' {...props}/>
  case 'Completed, Cancelled':
    return <Icon name='stop circle' color='green' {...props}/>
  case 'Completed, Errored':
    return <Icon name='x' color='red' {...props}/>
  default:
    return <Icon name='question circle' color='yellow' {...props}/>
  }
}

const SearchStatusIcon = ({ state, ...props }) => 
  <Popup content={state} trigger={getIcon({ state, ...props })}/>

export default SearchStatusIcon;