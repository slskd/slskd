import React from 'react';
import './Rooms.css';

import {
  Icon, Button, Menu
} from 'semantic-ui-react';
import RoomJoinModal from './RoomJoinModal';

const RoomMenu = ({ joined, active, onRoomChange, ...rest }) => {
  const names = [...joined];
  const isActive = (name) => active === name;

  return (
    <Menu className='room-menu' size='large'>
      {names.map((name, index) => 
        <Menu.Item
          className={`menu-item ${isActive(name) ? 'menu-active' : ''}`}
          key={index}
          name={name}
          active={isActive(name)}
          onClick={() => onRoomChange(name)}
        >
          <Icon name='circle' size='tiny' color='green'/>
          {name}
        </Menu.Item>
      )}
      <Menu.Menu position='right'>
        <RoomJoinModal
          trigger={
            <Button icon className='add-button'><Icon name='plus'/></Button>
          }
          centered
          size='small'
          {...rest}
        />
      </Menu.Menu>
    </Menu>
  )
}

export default RoomMenu;