import React from 'react';

import { useMediaQuery } from 'react-responsive';
import { Button, Icon, Popup } from 'semantic-ui-react';

const ShrinkableButton = ({ icon, loading, mediaQuery, children, ...rest }) => {
  const shouldShrink = useMediaQuery({ query: mediaQuery });

  if (!shouldShrink) {
    return (
      <Button {...rest}>
        <Icon name={icon} loading={loading}/>
        {children}
      </Button>
    );
  }

  return (
    <Popup
      content={children}
      trigger={
        <Button icon {...rest}>
          <Icon name={icon} loading={loading}/>
        </Button>
      }
    />
  );
};

export default ShrinkableButton;