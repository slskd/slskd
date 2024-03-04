import React from 'react';
import { useMediaQuery } from 'react-responsive';
import { Button, Icon, Popup } from 'semantic-ui-react';

const ShrinkableButton = ({ children, icon, loading, mediaQuery, ...rest }) => {
  const shouldShrink = useMediaQuery({ query: mediaQuery });

  if (!shouldShrink) {
    return (
      <Button {...rest}>
        <Icon
          loading={loading}
          name={icon}
        />
        {children}
      </Button>
    );
  }

  return (
    <Popup
      content={children}
      trigger={
        <Button
          icon
          {...rest}
        >
          <Icon
            loading={loading}
            name={icon}
          />
        </Button>
      }
    />
  );
};

export default ShrinkableButton;
