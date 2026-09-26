import UserView from '../Users/UserView';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Button } from 'semantic-ui-react';

const widthKey = 'slskd-user-panel-width';
const minWidth = 360;
const defaultWidth = 560;

const clampWidth = (width) =>
  Math.round(Math.max(minWidth, Math.min(width, window.innerWidth * 0.95)));

const loadWidth = () => {
  const saved = Number.parseInt(localStorage.getItem(widthKey), 10);
  return Number.isFinite(saved) ? saved : defaultWidth;
};

// a drawer on the right side of the screen showing a user's profile and
// shared files, so they can be looked at without leaving the current page
const UserPanel = ({ onClose, onOpenPage, onTabChange, tab, username }) => {
  const panelRef = useRef();
  const [width, setWidth] = useState(loadWidth);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    const onKeyDown = (event) => {
      // leave escape to any open modal or menu first
      const overlayOpen =
        event.defaultPrevented ||
        document.body.classList.contains('dimmed') ||
        document.querySelector('.user-context-menu') ||
        document.querySelector('.ui.dropdown.active') ||
        document.querySelector('.ui.popup.visible');

      if (event.key === 'Escape' && !overlayOpen) {
        onClose();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  // on wide screens the page makes room for the panel instead of being covered
  // by it; on narrow ones it overlays the page
  useEffect(() => {
    const root = document.documentElement;

    const update = () => {
      const panelWidth = clampWidth(width);
      root.style.setProperty('--user-panel-width', `${panelWidth}px`);
      document.body.classList.toggle(
        'user-panel-docked',
        window.innerWidth - panelWidth >= 640,
      );
    };

    update();
    window.addEventListener('resize', update);

    return () => {
      window.removeEventListener('resize', update);
      document.body.classList.remove('user-panel-docked');
      root.style.removeProperty('--user-panel-width');
    };
  }, [width]);

  const startResize = useCallback((event) => {
    event.preventDefault();
    const startX = event.clientX;
    const startWidth = panelRef.current.getBoundingClientRect().width;
    let latest = startWidth;

    const onMove = (moveEvent) => {
      latest = clampWidth(startWidth + startX - moveEvent.clientX);
      setWidth(latest);
    };

    const onUp = () => {
      document.removeEventListener('pointermove', onMove);
      document.removeEventListener('pointerup', onUp);
      document.body.classList.remove('user-panel-resizing');
      localStorage.setItem(widthKey, String(latest));
    };

    document.body.classList.add('user-panel-resizing');
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
  }, []);

  const resizeWithKeyboard = (event) => {
    const step = event.shiftKey ? 100 : 20;
    let next;

    if (event.key === 'ArrowLeft') next = clampWidth(width + step);
    if (event.key === 'ArrowRight') next = clampWidth(width - step);

    if (next) {
      event.preventDefault();
      setWidth(next);
      localStorage.setItem(widthKey, String(next));
    }
  };

  return (
    <aside
      aria-label={`${username}'s profile`}
      className="user-panel"
      ref={panelRef}
      style={{ width: clampWidth(width) }}
    >
      <button
        aria-label="Resize panel; use the left and right arrow keys"
        className="user-panel-resize-handle"
        onDoubleClick={() => {
          setWidth(defaultWidth);
          localStorage.removeItem(widthKey);
        }}
        onKeyDown={resizeWithKeyboard}
        onPointerDown={startResize}
        title="Drag to resize, double click to reset"
        type="button"
      />
      <header className="user-panel-header">
        <h2 className="user-panel-title">{username}</h2>
        <Button.Group
          className="user-panel-header-buttons"
          size="mini"
        >
          {tab === 'profile' && (
            <Button
              aria-label="Refresh profile"
              icon="refresh"
              onClick={() => setRefreshKey((key) => key + 1)}
              title="Refresh profile"
            />
          )}
          <Button
            aria-label="Open in a tab on the Users page"
            icon="window restore outline"
            onClick={onOpenPage}
            title="Open in a tab on the Users page"
          />
          <Button
            aria-label="Close panel"
            icon="close"
            onClick={onClose}
            title="Close (Esc)"
          />
        </Button.Group>
      </header>
      <div className="user-panel-body">
        <UserView
          onTabChange={onTabChange}
          refreshKey={refreshKey}
          tab={tab}
          username={username}
        />
      </div>
    </aside>
  );
};

export default UserPanel;
