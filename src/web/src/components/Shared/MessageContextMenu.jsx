import PropTypes from 'prop-types';
import React from 'react';
import { Button, Portal } from 'semantic-ui-react';

/**
 * Reusable context menu component for message actions
 * @param {Object} props - Component props
 * @param {boolean} props.open - Whether the menu is open
 * @param {number} props.x - X position for the menu
 * @param {number} props.y - Y position for the menu
 * @param {Array} props.actions - Array of action objects with { label, onClick, icon? }
 */
const MessageContextMenu = ({ actions = [], open, x, y }) => {
  if (!open) return null;

  return (
    <Portal open={open}>
      <div
        className="ui vertical buttons popup-menu"
        style={{
          left: x,
          maxHeight: `calc(100vh - ${y}px)`,
          top: y,
        }}
      >
        {actions.map((action, index) => (
          <Button
            className="ui compact button popup-option"
            icon={action.icon}
            key={index}
            onClick={action.onClick}
          >
            {action.label}
          </Button>
        ))}
      </div>
    </Portal>
  );
};

MessageContextMenu.propTypes = {
  actions: PropTypes.arrayOf(
    PropTypes.shape({
      icon: PropTypes.string,
      label: PropTypes.string.isRequired,
      onClick: PropTypes.func.isRequired,
    }),
  ),
  open: PropTypes.bool.isRequired,
  x: PropTypes.number.isRequired,
  y: PropTypes.number.isRequired,
};

export default MessageContextMenu;
