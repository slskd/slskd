import React from 'react';

import {
  Button,
  Dropdown,
} from 'semantic-ui-react';

import ShrinkableButton from './ShrinkableButton';

const ShrinkableDropdownButton = ({ 
  color, 
  icon,
  mediaQuery, 
  disabled,
  loading, 
  options, 
  onClick, 
  onChange, 
  hidden, 
  children,
}) => {
  if (hidden) {
    return <></>;
  }

  return (
    <Button.Group color={color}>
      <ShrinkableButton
        icon={icon}
        onClick={onClick}
        mediaQuery={mediaQuery}
        disabled={disabled}
        loading={loading}
      >{children}</ShrinkableButton>
      <Dropdown
        disabled={disabled}
        className='button icon'
        options={options}
        onChange={onChange}
        trigger={<></>}
      />
    </Button.Group>
  );
}

export default ShrinkableDropdownButton;