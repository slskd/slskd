import React from 'react';
import { useMediaQuery } from 'react-responsive';

import {
  Segment,
  Button,
  Header,
  Icon,
} from 'semantic-ui-react';
import SearchStatusIcon from '../SearchStatusIcon';

const SearchDetailHeader = ({ search, loading, onCreate, onStop, onRemove }) => {
  const isSmallScreen = useMediaQuery({ query: '(max-width: 899px)' });
  const isTinyScreen = useMediaQuery({ query: '(max-width: 684px)'});

  const { searchText, isComplete, state } = search;

  const stopOrRemove = () => {
    if (isComplete) {
      onRemove(search);
    } else {
      onStop(search);
    }
  }

  const RefreshButton = ({ ...props }) => 
    <Button 
      disabled={loading}
      icon={isSmallScreen && !isTinyScreen}
      onClick={() => onCreate({ search: searchText, navigate: true })}
    >
      <Icon name='refresh'/>{(!isSmallScreen || isTinyScreen) && 'Search Again'}
    </Button>

  const StopOrDeleteButton = ({ ...props }) => 
    <Button 
      negative
      disabled={loading}
      onClick={stopOrRemove}
      floated={isTinyScreen ? 'right' : ''}
      icon={isSmallScreen && !isTinyScreen}
    >
      <Icon name={isComplete ? 'trash alternate' : 'stop circle'}/>
      {(!isSmallScreen || isTinyScreen) && (isComplete ? 'Delete' : 'Stop')}
    </Button>

  return (
    <>
      <Segment className='search-segment' raised>
        <Header className='search-detail-header'>
          <SearchStatusIcon state={state}/>{searchText}
        </Header>
        {!isTinyScreen && <div className='search-detail-action-button'>
          <RefreshButton/>
          <StopOrDeleteButton/>
        </div>}
      </Segment>
      {isTinyScreen && <Segment>
        <RefreshButton/>
        <StopOrDeleteButton/>
      </Segment>}
    </>
  )
};

export default SearchDetailHeader;