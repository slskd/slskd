import React from 'react';
import './Chat.css';

import {
  Icon, Button, Label, Menu
} from 'semantic-ui-react';
import SendMessageModal from './SendMessageModal';

const ChatMenu = ({ conversations, active, onConversationChange, ...rest }) => {
  const names = Object.keys(conversations);

  const unread = Object.entries(conversations).reduce((acc, [name, messages]) => {
    acc[name] = messages.filter(message => !message.acknowledged)
    return acc;
  }, {});

  const isActive = (name) => active === name;

  return (
    <Menu className='conversation-menu' size='large'>
      {names.map((name, index) => 
        <Menu.Item
          className={`menu-item ${isActive(name) ? 'menu-active' : ''}`}
          key={index}
          name={name}
          active={isActive(name)}
          onClick={() => onConversationChange(name)}
        >
          <Icon name='circle' size='tiny' color='green'/>
          {name}
          {(unread[name] || []).length === 0 ? '' :
            <Label size='tiny' color='red'>{(unread[name] || []).length}</Label>
          }
        </Menu.Item>
      )}
      <Menu.Menu position='right'>
        <SendMessageModal
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

export default ChatMenu;