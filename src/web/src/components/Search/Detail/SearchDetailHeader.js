import React from 'react';
import { useMediaQuery } from 'react-responsive';

import {
  Segment,
  Button,
  Header,
  Icon,
} from 'semantic-ui-react';
import SearchStatusIcon from '../SearchStatusIcon';

const SearchDetailHeader = ({
  search,
  loading,
  loaded,
  creating,
  removing,
  stopping,
  disabled,
  onCreate,
  onStop,
  onRemove,
}) => {
  const isSmallScreen = useMediaQuery({ query: '(max-width: 899px)' });
  const isTinyScreen = useMediaQuery({ query: '(max-width: 684px)'});

  const { searchText, isComplete, state } = search;
  const working = loading || creating || removing || stopping;

  const stopOrRemove = () => {
    if (isComplete) {
      onRemove(search);
    } else {
      onStop(search);
    }
  }

  const RefreshButton = () => loaded &&
    <Button 
      disabled={disabled || working}
      icon={isSmallScreen && !isTinyScreen}
      onClick={() => onCreate({ search: searchText, navigate: true })}
      loading={creating}
    >
      <Icon name='refresh'/>{(!isSmallScreen || isTinyScreen) && 'Search Again'}
    </Button>

  const StopOrDeleteButton = () => 
    <Button 
      negative
      disabled={working}
      onClick={stopOrRemove}
      loading={removing || stopping}
      floated={isTinyScreen ? 'right' : undefined}
      icon={isSmallScreen && !isTinyScreen}
    >
      <Icon name={isComplete ? 'trash alternate' : 'stop circle'}/>
      {(!isSmallScreen || isTinyScreen) && ((loaded && isComplete) ? 'Delete' : 'Stop')}
    </Button>

  // if the screen is full width, display the header and action buttons in the same segment, with full
  // button text.  if the screen is between 684 and 899 pixels, display the buttons with no text.
  // if the screen is less than 684 pixels, display the action buttons in a new segment, with full text.
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