import ShrinkableButton from './ShrinkableButton';
import React from 'react';
import { Button, Dropdown } from 'semantic-ui-react';

const ShrinkableDropdownButton = ({
  children,
  color,
  disabled,
  hidden,
  icon,
  loading,
  mediaQuery,
  onChange,
  onClick,
  options,
}) => {
  if (hidden) {
    return null;
  }

  return (
    <Button.Group color={color}>
      <ShrinkableButton
        disabled={disabled}
        icon={icon}
        loading={loading}
        mediaQuery={mediaQuery}
        onClick={onClick}
      >
        {children}
      </ShrinkableButton>
      <Dropdown
        className="button icon"
        disabled={disabled}
        onChange={onChange}
        options={options}
        trigger={null}
      />
    </Button.Group>
  );
};

export default ShrinkableDropdownButton;
