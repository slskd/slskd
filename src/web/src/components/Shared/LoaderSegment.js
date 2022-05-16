import React from 'react';

import { Segment, Loader } from 'semantic-ui-react';

const LoaderSegment = ({ caption }) => (
  <Segment className='loader-segment' placeholder basic>
    <Loader active size='large'>{caption}</Loader>
  </Segment>
);

export default LoaderSegment;