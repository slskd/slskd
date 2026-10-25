import React, { useEffect, useRef, useState } from 'react';
import { Button, Portal } from 'semantic-ui-react';

/**
 * Reusable context menu component for message actions
 * @param {object} props - Component props
 * @param {boolean} props.open - Whether the menu is open
 * @param {number} props.x - X position for the menu
 * @param {number} props.y - Y position for the menu
 * @param {Array} props.actions - Array of action objects with { label, handleClick, icon? }
 */
const MessageContextMenu = ({ actions = [], open, x, y }) => {
  const menuReference = useRef(null);
  const [position, setPosition] = useState({ left: x, top: y });

  useEffect(() => {
    if (open && menuReference.current) {
      const menuHeight = menuReference.current.offsetHeight;
      const menuWidth = menuReference.current.offsetWidth;
      const viewportHeight = window.innerHeight;
      const viewportWidth = window.innerWidth;

      // Calculate if menu would overflow bottom of viewport
      const wouldOverflowBottom = y + menuHeight > viewportHeight;
      // Calculate if menu would overflow right of viewport
      const wouldOverflowRight = x + menuWidth > viewportWidth;

      // Adjust vertical position: if it would overflow, position above cursor
      const adjustedTop = wouldOverflowBottom ? Math.max(0, y - menuHeight) : y;

      // Adjust horizontal position: if it would overflow, align right edge with cursor
      const adjustedLeft = wouldOverflowRight ? Math.max(0, x - menuWidth) : x;

      setPosition({ left: adjustedLeft, top: adjustedTop });
    }
  }, [open, x, y]);

  if (!open) return null;

  return (
    <Portal open={open}>
      <div
        className="ui vertical buttons popup-menu"
        ref={menuReference}
        style={{
          left: position.left,
          maxHeight: '90vh',
          overflowY: 'auto',
          top: position.top,
        }}
      >
        {actions.map((action) => (
          <Button
            className="ui compact button popup-option"
            icon={action.icon}
            key={action.label}
            onClick={action.handleClick}
          >
            {action.label}
          </Button>
        ))}
      </div>
    </Portal>
  );
};

export default MessageContextMenu;
