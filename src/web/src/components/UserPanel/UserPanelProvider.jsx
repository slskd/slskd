import './UserPanel.css';
import { urlBase } from '../../config';
import { copyToClipboard } from '../../lib/util';
import ComposeMessageModal from './ComposeMessageModal';
import UserContextMenu from './UserContextMenu';
import UserPanel from './UserPanel';
import UserPanelContext from './UserPanelContext';
import React, { useCallback, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { useHistory } from 'react-router-dom';
import { toast } from 'react-toastify';

const copy = async (username) => {
  await copyToClipboard(username);
  toast.info(`Copied ${username}`);
};

const UserPanelProvider = ({ children }) => {
  const history = useHistory();
  const [panel, setPanel] = useState({ tab: 'profile', username: undefined });
  const [menu, setMenu] = useState();
  const [composeTo, setComposeTo] = useState();

  const openUser = useCallback(
    (username, { tab = 'profile' } = {}) => setPanel({ tab, username }),
    [],
  );

  const closeUser = useCallback(
    () => setPanel((previous) => ({ ...previous, username: undefined })),
    [],
  );

  const openUserPage = useCallback(
    (username) =>
      history.push(`${urlBase}/users/${encodeURIComponent(username)}`),
    [history],
  );

  const value = useMemo(
    () => ({
      closeUser,
      composeMessage: (username) => setComposeTo(username),
      openMenu: (username, { x, y }) => setMenu({ username, x, y }),
      openUser,
      openUserPage,
    }),
    [closeUser, openUser, openUserPage],
  );

  const closeMenu = useCallback(() => setMenu(undefined), []);

  const menuItems = (username) => [
    {
      icon: 'user',
      label: 'View Profile',
      onSelect: () => openUser(username),
    },
    {
      icon: 'folder open',
      label: 'Browse Files',
      onSelect: () => openUser(username, { tab: 'files' }),
    },
    {
      icon: 'comment',
      label: 'Send Message…',
      onSelect: () => setComposeTo(username),
    },
    {
      icon: 'window restore outline',
      label: 'Open in Users Tab',
      onSelect: () => openUserPage(username),
    },
    {
      icon: 'copy',
      label: 'Copy Username',
      onSelect: () => copy(username),
    },
  ];

  return (
    <UserPanelContext.Provider value={value}>
      {children}
      {/* rendered into body: the app content area is transformed, which would
          make position: fixed scroll along with the page */}
      {panel.username &&
        createPortal(
          <UserPanel
            onClose={closeUser}
            onOpenPage={() => {
              openUserPage(panel.username);
              closeUser();
            }}
            onTabChange={(tab) =>
              setPanel((previous) => ({ ...previous, tab }))
            }
            tab={panel.tab}
            username={panel.username}
          />,
          document.body,
        )}
      {menu && (
        <UserContextMenu
          items={menuItems(menu.username)}
          onClose={closeMenu}
          username={menu.username}
          x={menu.x}
          y={menu.y}
        />
      )}
      <ComposeMessageModal
        onClose={() => setComposeTo(undefined)}
        username={composeTo}
      />
    </UserPanelContext.Provider>
  );
};

export default UserPanelProvider;
