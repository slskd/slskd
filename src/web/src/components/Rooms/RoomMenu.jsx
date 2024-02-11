import './Rooms.css';
import RoomJoinModal from './RoomJoinModal';
import React from 'react';
import { Button, Icon, Menu } from 'semantic-ui-react';

const RoomMenu = ({ active, joined, onRoomChange, ...rest }) => {
  const names = [...joined];
  const isActive = (name) => active === name;

  return (
    <Menu
      className="room-menu"
      size="large"
    >
      {names.map((name, index) => (
        <Menu.Item
          active={isActive(name)}
          className={`menu-item ${isActive(name) ? 'menu-active' : ''}`}
          key={index}
          name={name}
          onClick={() => onRoomChange(name)}
        >
          <Icon
            color="green"
            name="circle"
            size="tiny"
          />
          {name}
        </Menu.Item>
      ))}
      <Menu.Menu position="right">
        <RoomJoinModal
          centered
          size="small"
          trigger={
            <Button
              className="add-button"
              icon
            >
              <Icon name="plus" />
            </Button>
          }
          {...rest}
        />
      </Menu.Menu>
    </Menu>
  );
};

export default RoomMenu;
