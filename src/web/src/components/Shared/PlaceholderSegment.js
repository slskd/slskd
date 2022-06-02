import React from 'react';

import { Segment, Header, Icon } from 'semantic-ui-react';

const PlaceholderSegment = ({ icon, caption, size, ...rest }) => {
  const className = size === 'small' ? 'placeholder-segment-small' : 'placeholder-segment';

  return (
    <Segment className={className} placeholder basic {...rest}>
      <Header icon>
        <Icon name={icon}/>
        {caption}
      </Header>
    </Segment>);
};

export default PlaceholderSegment;