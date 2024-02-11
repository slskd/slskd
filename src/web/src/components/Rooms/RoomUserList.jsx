import './Rooms.css';
import React, { useMemo } from 'react';
import { Flag, Icon, List, Popup } from 'semantic-ui-react';

const RoomUserList = ({ users }) => {
  const getFlag = (user) => {
    if (!(user || {}).countryCode)
      return (
        <Icon
          className="unknown-user-flag"
          name="question"
        />
      );

    return <Flag name={user.countryCode.toLowerCase()} />;
  };

  const getDetails = (user) => {
    return user.countryCode || '?';
  };

  const sortedUsers = useMemo(() => {
    const filtered = [...users]
      .sort((a, b) => a.username.localeCompare(b.username))
      .reduce(
        (accumulator, user) => {
          (user.status === 'Online'
            ? accumulator.online
            : accumulator.offline
          ).push(user);
          return accumulator;
        },
        { offline: [], online: [] },
      );

    return [...filtered.online, ...filtered.offline];
  }, [users]);

  return (
    <List>
      {sortedUsers.map((user, index) => (
        <List.Item
          className={user.self ? 'room-user-self' : ''}
          key={index}
        >
          <List.Content style={{ opacity: user.status === 'Online' ? 1 : 0.5 }}>
            <Popup
              content={getDetails(user)}
              trigger={getFlag(user)}
            />
            {user.username}
          </List.Content>
        </List.Item>
      ))}
    </List>
  );
};

export default RoomUserList;
