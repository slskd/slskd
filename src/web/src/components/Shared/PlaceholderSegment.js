import React from 'react';

import { Segment, Header, Icon } from 'semantic-ui-react';

const PlaceholderSegment = ({ icon, caption, ...rest }) => (
  <Segment className='placeholder-segment' placeholder basic {...rest}>
  <Header icon>
    <Icon name={icon}/>
    {caption}
  </Header>
</Segment>
);

export default PlaceholderSegment;