import React from 'react';

import { useMediaQuery } from 'react-responsive';
import { Button, Icon, Popup } from 'semantic-ui-react';

const ShrinkableButton = ({ iconName, mediaQuery, children, ...rest }) => {
  const shouldShrink = useMediaQuery({ query: mediaQuery })

  if (!shouldShrink) {
    return (
      <Button {...rest}>
        <Icon name={iconName}/>
        {children}
      </Button>
    )
  }

  return (
    <Popup
      content={children}
      trigger={
        <Button icon {...rest}>
          <Icon name={iconName}/>
        </Button>
      }
    />
  )
}

export default ShrinkableButton;