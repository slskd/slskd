import './Chat.css';
import { activeChatKey } from '../../config';
import * as chat from '../../lib/chat';
import * as userActions from '../../lib/userActions';
import MessageContextMenu from '../Shared/MessageContextMenu';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import ChatMenu from './ChatMenu';
import React, { Component, createRef } from 'react';
import { withRouter } from 'react-router-dom';
import {
  Card,
  Dimmer,
  Icon,
  Input,
  List,
  Loader,
  Ref,
  Segment,
} from 'semantic-ui-react';

const initialState = {
  active: '',
  contextMenu: {
    message: null,
    open: false,
    x: 0,
    y: 0,
  },
  conversations: {},
  interval: undefined,
  loading: false,
};

class Chat extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    this.setState(
      {
        active: sessionStorage.getItem(activeChatKey) || '',
        interval: window.setInterval(this.fetchConversations, 5_000),
      },
      async () => {
        await this.fetchConversations();
        this.selectConversation(
          this.state.active || this.getFirstConversation(),
        );
        document.addEventListener('click', this.handleCloseContextMenu);
      },
    );
  }

  componentWillUnmount() {
    clearInterval(this.state.interval);
    document.removeEventListener('click', this.handleCloseContextMenu);
    this.setState({ interval: undefined });
  }

  listRef = createRef();

  messageRef = undefined;

  getFirstConversation = () => {
    const names = Object.keys(this.state.conversations);
    return names.length > 0 ? names[0] : '';
  };

  fetchConversations = async () => {
    // fetch all of the active conversations (but no messages)
    let conversations = await chat.getAll();

    // turn into a map, keyed by username
    // if there are no active conversations, set to an empty map
    if (conversations.length === 0) {
      conversations = {};
    } else {
      conversations = conversations.reduce((map, current) => {
        map[current.username] = current;
        return map;
      }, {});
    }

    const { active } = this.state;
    const activeConversation = conversations[active];

    // check to see if the active chat is still active
    // this will happen whenever a chat is closed/removed
    if (activeConversation) {
      console.log('active?', activeConversation);
      // *before* fetching messages, ack any unacked messages
      // for the active chat
      if (activeConversation.hasUnAcknowledgedMessages === true) {
        await this.acknowledgeMessages(active);
      }

      conversations = {
        ...conversations,
        [active]: await chat.get({ username: active }),
      };
    }

    this.setState({ conversations }, () => {
      // if a chat isn't active or the active chat is closed,
      // select the first conversation, if there is one
      if (!this.state.conversations[this.state.active]) {
        this.selectConversation(this.getFirstConversation());
      }
    });
  };

  acknowledgeMessages = async (username) => {
    if (!username || username === '') return;
    await chat.acknowledge({ username });
  };

  sendMessage = async (username, message) => {
    if (!username || !message || username === '') return;
    await chat.send({ message, username });
  };

  sendReply = async () => {
    const { active } = this.state;
    const message = this.messageRef.current.value;

    if (!this.validInput()) {
      return;
    }

    await this.sendMessage(active, message);
    this.messageRef.current.value = '';

    // force a refresh to append the message
    // we could probably do this in the browser but we can be lazy
    this.fetchConversations();
  };

  validInput = () =>
    (this.state.active || '').length > 0 &&
    (
      (this.messageRef &&
        this.messageRef.current &&
        this.messageRef.current.value) ||
      ''
    ).length > 0;

  focusInput = () => {
    this.messageRef.current.focus();
  };

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    const dtfUS = new Intl.DateTimeFormat('en', {
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      month: 'numeric',
    });

    return dtfUS.format(date);
  };

  selectConversation = (username) => {
    this.setState(
      {
        active: username,
        loading: true,
      },
      async () => {
        const { active, conversations } = this.state;

        sessionStorage.setItem(activeChatKey, active);

        this.setState(
          {
            conversations:
              active === ''
                ? conversations
                : {
                    ...conversations,
                    [active]: await chat.get({ username: active }),
                  },
            loading: false,
          },
          () => {
            try {
              this.listRef.current.lastChild.scrollIntoView();
            } catch {
              // no-op
            }
          },
        );
      },
    );
  };

  initiateConversation = async (username, message) => {
    await this.sendMessage(username, message);
    await this.fetchConversations();
    this.selectConversation(username);
  };

  deleteConversation = async (username) => {
    await chat.remove({ username });
    await this.fetchConversations();
    this.selectConversation(this.getFirstConversation());
  };

  handleContextMenu = (clickEvent, message) => {
    clickEvent.preventDefault();
    this.setState({
      contextMenu: {
        message,
        open: true,
        x: clickEvent.pageX,
        y: clickEvent.pageY,
      },
    });
  };

  handleCloseContextMenu = () => {
    this.setState((previousState) => ({
      contextMenu: {
        ...previousState.contextMenu,
        open: false,
      },
    }));
  };

  handleReply = () => {
    const { message } = this.state.contextMenu.message;
    this.messageRef.current.value = `${message} --> `;
    this.focusInput();
    this.handleCloseContextMenu();
  };

  handleUserProfile = () => {
    const username = this.state.contextMenu?.message?.username;
    if (!username) return;
    userActions.navigateToUserProfile(this.props.history, username);
    this.handleCloseContextMenu();
  };

  handleBrowseShares = () => {
    const username = this.state.contextMenu?.message?.username;
    if (!username) return;
    userActions.navigateToBrowseShares(this.props.history, username);
    this.handleCloseContextMenu();
  };

  handleIgnoreUser = async () => {
    const username = this.state.contextMenu?.message?.username;
    if (!username) return;
    await userActions.ignoreUser(username);
    this.handleCloseContextMenu();
  };

  getContextMenuActions = () => {
    return [
      {
        handleClick: this.handleReply,
        label: 'Reply',
      },
      {
        handleClick: this.handleUserProfile,
        label: 'User Profile',
      },
      {
        handleClick: this.handleBrowseShares,
        label: 'Browse Shares',
      },
      {
        handleClick: this.handleIgnoreUser,
        label: 'Ignore User',
      },
    ];
  };

  render() {
    const { active, conversations = [], loading } = this.state;
    const messages = conversations[active]?.messages || [];
    const { user } = this.props.state;

    return (
      <div className="chats">
        <Segment
          className="chat-segment"
          raised
        >
          <div className="chat-segment-icon">
            <Icon
              name="comment"
              size="big"
            />
          </div>
          <ChatMenu
            active={active}
            conversations={conversations}
            initiateConversation={this.initiateConversation}
            onConversationChange={(name) => this.selectConversation(name)}
          />
        </Segment>
        {Boolean(active) === false ? (
          <PlaceholderSegment
            caption="No chats to display"
            icon="comment"
          />
        ) : (
          <Card
            className="chat-active-card"
            raised
          >
            <Card.Content onClick={() => this.focusInput()}>
              <Card.Header>
                <Icon
                  color="green"
                  name="circle"
                />
                {active}
                <Icon
                  className="close-button"
                  color="red"
                  link
                  name="close"
                  onClick={() => this.deleteConversation(active)}
                />
              </Card.Header>
              <div className="chat">
                {loading ? (
                  <Dimmer
                    active
                    inverted
                  >
                    <Loader inverted />
                  </Dimmer>
                ) : (
                  <Segment.Group>
                    <Segment className="chat-history">
                      <Ref innerRef={this.listRef}>
                        <List>
                          {messages.map((message) => (
                            <div
                              key={`${message.timestamp}+${message.message}`}
                              onContextMenu={(clickEvent) =>
                                this.handleContextMenu(clickEvent, message)
                              }
                            >
                              <List.Content
                                className={`chat-message ${message.direction === 'Out' ? 'chat-message-self' : ''}`}
                              >
                                <span className="chat-message-time">
                                  {this.formatTimestamp(message.timestamp)}
                                </span>
                                <span className="chat-message-name">
                                  {message.direction === 'Out'
                                    ? user.username
                                    : message.username}
                                  :{' '}
                                </span>
                                <span className="chat-message-message">
                                  {message.message}
                                </span>
                              </List.Content>
                            </div>
                          ))}
                          <List.Content id="chat-history-scroll-anchor" />
                        </List>
                      </Ref>
                    </Segment>
                    <Segment className="chat-input">
                      <Input
                        action={{
                          className: 'chat-message-button',
                          disabled: !this.validInput(),
                          icon: (
                            <Icon
                              color="green"
                              name="send"
                            />
                          ),
                          onClick: this.sendMessage,
                        }}
                        fluid
                        input={
                          <input
                            autoComplete="off"
                            data-lpignore="true"
                            id="chat-message-input"
                            type="text"
                          />
                        }
                        onKeyUp={(event) =>
                          event.key === 'Enter' ? this.sendReply() : ''
                        }
                        ref={(input) =>
                          (this.messageRef = input && input.inputRef)
                        }
                        transparent
                      />
                    </Segment>
                  </Segment.Group>
                )}
              </div>
            </Card.Content>
          </Card>
        )}
        <MessageContextMenu
          actions={this.getContextMenuActions()}
          open={this.state.contextMenu.open}
          x={this.state.contextMenu.x}
          y={this.state.contextMenu.y}
        />
      </div>
    );
  }
}

export default withRouter(Chat);
