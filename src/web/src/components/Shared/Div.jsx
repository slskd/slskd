import React from 'react';

const Div = ({ children, hidden, ...rest }) => {
  if (hidden) {
    return null;
  }

  return <div {...rest}>{children}</div>;
};

export default Div;
