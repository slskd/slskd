import SearchStatusIcon from '../SearchStatusIcon';
import React from 'react';
import { useMediaQuery } from 'react-responsive';
import { Button, Header, Icon, Segment } from 'semantic-ui-react';

const RefreshButton = ({
  creating,
  disabled,
  isSmallScreen,
  isTinyScreen,
  loaded,
  onCreate,
  searchText,
  working,
}) =>
  loaded && (
    <Button
      disabled={disabled || working}
      icon={isSmallScreen && !isTinyScreen}
      loading={creating}
      onClick={() => onCreate({ navigate: true, search: searchText })}
    >
      <Icon name="refresh" />
      {(!isSmallScreen || isTinyScreen) && 'Search Again'}
    </Button>
  );

const StopOrDeleteButton = ({
  isComplete,
  isSmallScreen,
  isTinyScreen,
  loaded,
  removing,
  stopOrRemove,
  stopping,
  working,
}) => (
  <Button
    disabled={working}
    floated={isTinyScreen ? 'right' : undefined}
    icon={isSmallScreen && !isTinyScreen}
    loading={removing || stopping}
    negative
    onClick={stopOrRemove}
  >
    <Icon name={isComplete ? 'trash alternate' : 'stop circle'} />
    {(!isSmallScreen || isTinyScreen) &&
      (loaded && isComplete ? 'Delete' : 'Stop')}
  </Button>
);

const SearchDetailHeader = ({
  creating,
  disabled,
  loaded,
  loading,
  onCreate,
  onRemove,
  onStop,
  removing,
  search,
  stopping,
}) => {
  const isSmallScreen = useMediaQuery({ query: '(max-width: 899px)' });
  const isTinyScreen = useMediaQuery({ query: '(max-width: 684px)' });

  const { isComplete, searchText, state } = search;
  const working = loading || creating || removing || stopping;

  const stopOrRemove = () => {
    if (isComplete) {
      onRemove(search);
    } else {
      onStop(search);
    }
  };

  // if the screen is full width, display the header and action buttons in the same segment, with full
  // button text.  if the screen is between 684 and 899 pixels, display the buttons with no text.
  // if the screen is less than 684 pixels, display the action buttons in a new segment, with full text.
  return (
    <>
      <Segment
        className="search-detail-header-segment"
        raised
      >
        <Header>
          <SearchStatusIcon state={state} />
          {searchText}
        </Header>
        {!isTinyScreen && (
          <div className="search-detail-header-buttons">
            <RefreshButton
              creating={creating}
              disabled={disabled}
              isSmallScreen={isSmallScreen}
              isTinyScreen={isTinyScreen}
              loaded={loaded}
              onCreate={onCreate}
              searchText={searchText}
              working={working}
            />
            <StopOrDeleteButton
              isComplete={isComplete}
              isSmallScreen={isSmallScreen}
              isTinyScreen={isTinyScreen}
              loaded={loaded}
              removing={removing}
              stopOrRemove={stopOrRemove}
              stopping={stopping}
              working={working}
            />
          </div>
        )}
      </Segment>
      {isTinyScreen && (
        <Segment>
          <RefreshButton
            creating={creating}
            disabled={disabled}
            isSmallScreen={isSmallScreen}
            isTinyScreen={isTinyScreen}
            loaded={loaded}
            onCreate={onCreate}
            searchText={searchText}
            working={working}
          />
          <StopOrDeleteButton
            isComplete={isComplete}
            isSmallScreen={isSmallScreen}
            isTinyScreen={isTinyScreen}
            loaded={loaded}
            removing={removing}
            stopOrRemove={stopOrRemove}
            stopping={stopping}
            working={working}
          />
        </Segment>
      )}
    </>
  );
};

export default SearchDetailHeader;
