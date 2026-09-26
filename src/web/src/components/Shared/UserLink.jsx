import { useUserPanel } from '../UserPanel/UserPanelContext';
import React from 'react';

// a username that opens the user panel on click and the user context menu on
// right click, like usernames everywhere in nicotine+
const UserLink = ({ children, className = '', tab, username }) => {
  const panel = useUserPanel();

  if (!panel || !username) {
    return <span className={className}>{children ?? username}</span>;
  }

  return (
    <button
      className={`user-link ${className}`}
      onClick={(event) => {
        event.stopPropagation();
        panel.openUser(username, { tab });
      }}
      onContextMenu={(event) => {
        event.preventDefault();
        event.stopPropagation();
        panel.openMenu(username, { x: event.clientX, y: event.clientY });
      }}
      title={`View ${username}'s profile (right click for more)`}
      type="button"
    >
      {children ?? username}
    </button>
  );
};

export default UserLink;
