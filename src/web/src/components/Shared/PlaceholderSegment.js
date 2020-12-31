import React from 'react';

import { Segment, Header, Icon } from 'semantic-ui-react';

const PlaceholderSegment = ({ icon, caption }) => (
  <Segment className='placeholder-segment' placeholder basic>
  <Header icon>
    <Icon name={icon}/>
    {caption}
  </Header>
</Segment>
);

export default PlaceholderSegment;