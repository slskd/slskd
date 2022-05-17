import React from 'react';

import { Segment, Loader } from 'semantic-ui-react';

const LoaderSegment = ({ size = 'big', children, ...props }) => (
  <Segment className='loader-segment' placeholder basic>
    <Loader active size={size} {...props}>{children}</Loader>
  </Segment>
);

export default LoaderSegment;