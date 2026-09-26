import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Icon } from 'semantic-ui-react';

// the right click menu for usernames.  positioned at the pointer and kept
// inside the viewport; closes on outside click, scroll, resize, or escape
const UserContextMenu = ({ items, onClose, username, x, y }) => {
  const menuRef = useRef();
  const [position, setPosition] = useState({ left: x, top: y });

  useLayoutEffect(() => {
    const menu = menuRef.current;

    if (!menu) {
      return;
    }

    const { height, width } = menu.getBoundingClientRect();
    setPosition({
      left: Math.max(4, Math.min(x, window.innerWidth - width - 4)),
      top: Math.max(4, Math.min(y, window.innerHeight - height - 4)),
    });
    menu.querySelector('[role="menuitem"]')?.focus();
  }, [x, y]);

  useEffect(() => {
    const close = (event) => {
      // right clicking another username opens a menu for it; closing here
      // would immediately undo that
      const onUserLink =
        event.type === 'contextmenu' && event.target.closest?.('.user-link');

      if (!menuRef.current?.contains(event.target) && !onUserLink) {
        onClose();
      }
    };

    const closeNow = () => onClose();

    document.addEventListener('mousedown', close);
    document.addEventListener('contextmenu', close);
    window.addEventListener('resize', closeNow);
    window.addEventListener('scroll', closeNow, true);

    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('contextmenu', close);
      window.removeEventListener('resize', closeNow);
      window.removeEventListener('scroll', closeNow, true);
    };
  }, [onClose]);

  const onKeyDown = (event) => {
    const menuItems = [
      ...menuRef.current.querySelectorAll('[role="menuitem"]'),
    ];
    const index = menuItems.indexOf(document.activeElement);

    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      menuItems[(index + 1) % menuItems.length]?.focus();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      menuItems[(index - 1 + menuItems.length) % menuItems.length]?.focus();
    }
  };

  return createPortal(
    <div
      aria-label={`Actions for ${username}`}
      className="user-context-menu"
      onKeyDown={onKeyDown}
      ref={menuRef}
      role="menu"
      style={position}
      tabIndex={-1}
    >
      <div className="user-context-menu-title">{username}</div>
      {items.map(({ icon, label, onSelect }) => (
        <button
          className="user-context-menu-item"
          key={label}
          onClick={() => {
            onClose();
            onSelect();
          }}
          role="menuitem"
          type="button"
        >
          <Icon name={icon} />
          {label}
        </button>
      ))}
    </div>,
    document.body,
  );
};

export default UserContextMenu;
