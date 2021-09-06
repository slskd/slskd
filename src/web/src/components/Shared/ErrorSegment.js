import React from 'react';

import { Segment, Header, Icon } from 'semantic-ui-react';

const ErrorSegment = ({ icon, caption }) => (
  <Segment className='error-segment' placeholder basic>
  <Header icon>
    <Icon name='x' color='red'/>
    Error: {caption}
  </Header>
</Segment>
);

export default ErrorSegment;