import React from 'react';

import { Segment, Header, Icon } from 'semantic-ui-react';

const ErrorSegment = ({ icon = 'x', caption, suppressPrefix = false }) => (
  <Segment className='error-segment' placeholder basic>
    <Header icon>
      <Icon name={icon} color='red'/>
      {!suppressPrefix && 'Error: '}{caption}
    </Header>
  </Segment>
);

export default ErrorSegment;