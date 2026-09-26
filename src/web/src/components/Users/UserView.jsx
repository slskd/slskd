import UserFiles from './UserFiles';
import UserProfile from './UserProfile';
import React, { useEffect, useState } from 'react';
import { Menu } from 'semantic-ui-react';

// a user's profile and shared files, switchable with tabs.  used by the side
// panel and the users page
const UserView = ({ onTabChange, refreshKey, tab, username }) => {
  // the files tab browses the user on mount, so only mount it once it's
  // asked for, then keep it mounted so switching tabs doesn't browse again
  const [filesVisited, setFilesVisited] = useState(tab === 'files');

  useEffect(() => {
    setFilesVisited(tab === 'files');
  }, [username]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (tab === 'files') {
      setFilesVisited(true);
    }
  }, [tab]);

  return (
    <>
      <Menu
        className="user-view-tabs"
        pointing
        secondary
      >
        <Menu.Item
          active={tab === 'profile'}
          icon="user"
          name="Profile"
          onClick={() => onTabChange('profile')}
        />
        <Menu.Item
          active={tab === 'files'}
          icon="folder open"
          name="Files"
          onClick={() => onTabChange('files')}
        />
      </Menu>
      <div hidden={tab !== 'profile'}>
        <UserProfile
          onBrowse={() => onTabChange('files')}
          refreshKey={refreshKey}
          username={username}
        />
      </div>
      {filesVisited && (
        <div hidden={tab !== 'files'}>
          <UserFiles username={username} />
        </div>
      )}
    </>
  );
};

export default UserView;
