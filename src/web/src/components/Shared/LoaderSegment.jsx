import React from 'react';
import { Loader, Segment } from 'semantic-ui-react';

const LoaderSegment = ({ children, size = 'big', ...props }) => (
  <Segment
    basic
    className="loader-segment"
    placeholder
  >
    <Loader
      active
      size={size}
      {...props}
    >
      {children}
    </Loader>
  </Segment>
);

export default LoaderSegment;
