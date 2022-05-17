import React from 'react';

import {
  Segment,
  Button,
  Header,
  Icon,
} from 'semantic-ui-react';
import SearchStatusIcon from '../SearchStatusIcon';

const SearchDetailHeader = ({ search, loading, onStop, onRemove }) => {
  const { searchText, isComplete, state } = search;

  const stopOrRemove = () => {
    if (isComplete) {
      onRemove(search);
    } else {
      onStop(search);
    }
  }

  return (
    <Segment className='search-segment' raised>
      <Header className='search-detail-header'>
        <SearchStatusIcon state={state}/>
        {searchText}
      </Header>
      <Button
        className='search-detail-action-button'
        negative
        disabled={loading}
        onClick={stopOrRemove}
      >
        <Icon name={isComplete ? 'trash alternate' : 'stop circle'}/>
        {isComplete ? 'Delete' : 'Stop'} Search
      </Button>
    </Segment>
  )
};

export default SearchDetailHeader;