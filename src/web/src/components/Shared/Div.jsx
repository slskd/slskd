import React from 'react';

const Div = ({ hidden, children, ...rest }) => {
  if (hidden) {
    return <></>;
  }

  return (<div {...rest}>{children}</div>);
};

export default Div;