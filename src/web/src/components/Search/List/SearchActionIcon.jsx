import React from 'react';
import { Icon } from 'semantic-ui-react';

const SearchActionIcon = ({ loading, onRemove, onStop, search, ...props }) => {
  if (loading) {
    return (
      <Icon
        loading
        name="spinner"
        {...props}
      />
    );
  }

  if (search.state.includes('Completed')) {
    return (
      <Icon
        color="red"
        name="trash alternate"
        onClick={() => onRemove()}
        style={{ cursor: 'pointer' }}
      />
    );
  }

  return (
    <Icon
      color="red"
      name="stop circle"
      onClick={() => onStop()}
      style={{ cursor: 'pointer' }}
    />
  );
};

export default SearchActionIcon;
