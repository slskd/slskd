import React from 'react';

import {
  Segment,
  Button,
} from 'semantic-ui-react';

const SearchDetailHeader = ({ search, onBack, onStop }) => {
  return (
    <Segment className='search-segment' raised>
    {/* <Input
      input={<input placeholder="Search phrase" type="search" data-lpignore="true"></input>}
      size='big'
      ref={searchRef}
      disabled={true}
      className='search-input'
      placeholder="Search phrase"
      action={<Button icon='x' color='red' onClick={() => history.push(`/searches`)}/>}
    /> */}
    <Button
      negative
      icon={search?.isComplete ? 'arrow left' : 'stop circle'}
      onClick={() => {
        if (search?.isComplete) {
          onBack();
        } else {
          onStop(search);
        }
      }}
    />
  </Segment>
  )
};

export default SearchDetailHeader;