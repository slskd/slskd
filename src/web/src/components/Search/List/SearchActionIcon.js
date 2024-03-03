import React from 'react';

import { Icon } from 'semantic-ui-react';

const SearchActionIcon = ({ search, loading, onRemove, onStop,...props }) => {
  if (loading) {
    return (<Icon name='spinner' loading {...props}/>);
  }

  if (search.state.includes('Completed')
    || search.state.includes('Cancelled')
    || search.state.includes('TimedOut')
    || search.state.includes('ResponseLimitReached')
    || search.state.includes('FileLimitReached')
    || search.state.includes('Errored')) {
    return (<Icon
      name="trash alternate"
      color='red' 
      onClick={() => onRemove()}
      style={{ cursor: 'pointer' }}
    />);
  }

  return (<Icon
    name="stop circle"
    color="red"
    onClick={() => onStop()}
    style={{ cursor: 'pointer' }}
  />);
};

export default SearchActionIcon;