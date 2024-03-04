import './Chat.css';
import SendMessageModal from './SendMessageModal';
import React from 'react';
import { Button, Icon, Label, Menu } from 'semantic-ui-react';

const ChatMenu = ({ active, conversations, onConversationChange, ...rest }) => {
  const names = Object.keys(conversations);
  const isActive = (name) => active === name;

  return (
    <Menu
      className="conversation-menu"
      size="large"
    >
      {names.map((name) => (
        <Menu.Item
          active={isActive(name)}
          className={`menu-item ${isActive(name) ? 'menu-active' : ''}`}
          key={name}
          name={name}
          onClick={() => onConversationChange(name)}
        >
          <Icon
            color="green"
            name="circle"
            size="tiny"
          />
          {name}
          {conversations[name].hasUnAcknowledgedMessages && (
            <Label
              color="red"
              size="tiny"
            >
              {conversations[name].unAcknowledgedMessageCount}
            </Label>
          )}
        </Menu.Item>
      ))}
      <Menu.Menu position="right">
        <SendMessageModal
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

export default ChatMenu;
