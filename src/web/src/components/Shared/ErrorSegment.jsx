import React from 'react';
import { Header, Icon, Segment } from 'semantic-ui-react';

const ErrorSegment = ({ caption, icon = 'x', suppressPrefix = false }) => (
  <Segment
    basic
    className="error-segment"
    placeholder
  >
    <Header icon>
      <Icon
        color="red"
        name={icon}
      />
      {!suppressPrefix && 'Error: '}
      {caption}
    </Header>
  </Segment>
);

export default ErrorSegment;
