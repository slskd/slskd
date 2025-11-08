import { useCallback, useEffect, useState } from 'react';

/**
 * Custom hook for managing context menu state and behavior
 * @returns {object} Context menu state and handlers
 */
export const useContextMenu = () => {
  const [contextMenu, setContextMenu] = useState({
    data: null,
    open: false,
    x: 0,
    y: 0,
  });

  const handleOpen = useCallback((event, data) => {
    event.preventDefault();
    setContextMenu({
      data,
      open: true,
      x: event.pageX,
      y: event.pageY,
    });
  }, []);

  const handleClose = useCallback(() => {
    setContextMenu((previous) => ({
      ...previous,
      open: false,
    }));
  }, []);

  useEffect(() => {
    if (contextMenu.open) {
      document.addEventListener('click', handleClose);
      return () => {
        document.removeEventListener('click', handleClose);
      };
    }

    return undefined;
  }, [contextMenu.open, handleClose]);

  return {
    contextMenu,
    handleClose,
    handleOpen,
  };
};
