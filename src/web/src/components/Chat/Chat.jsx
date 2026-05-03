import './Chat.css';
import { activeChatKey } from '../../config';
import * as chat from '../../lib/chat';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import ChatMenu from './ChatMenu';
import React, { Component, createRef } from 'react';
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
  conversations: {},
  interval: undefined,
  loading: false,
  message: '',
};

const ChatMessageHistory = React.memo(
  ({ formatTimestamp, messages, selfUsername }) => {
    return (
      <>
        {messages.map((message) => (
          <List.Content
            className={`chat-message ${message.direction === 'Out' ? 'chat-message-self' : ''}`}
            key={`${message.timestamp}+${message.message}`}
          >
            <span className="chat-message-time">
              {formatTimestamp(message.timestamp)}
            </span>
            <span className="chat-message-name">
              {message.direction === 'Out' ? selfUsername : message.username}:
            </span>
            <span className="chat-message-message">{message.message}</span>
          </List.Content>
        ))}
        <List.Content id="chat-history-scroll-anchor" />
      </>
    );
  },
);

ChatMessageHistory.displayName = 'ChatMessageHistory';

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
      },
    );
  }

  componentWillUnmount() {
    clearInterval(this.state.interval);
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

    // the username '..' breaks nearly everything in slskd because it's a path traversal
    // sequence and no API endpoints that expect usernames in the path work
    // rather than letting this sit broken, i'm making the choice to filter it out
    // eslint-disable-next-line no-warning-comments
    // todo: remove this filter when the API can handle '..'
    conversations = conversations.filter((f) => f.username !== '..');

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

  sendReply = async () => {
    const { active, message } = this.state;

    if (!this.validInput()) {
      return;
    }

    await chat.send({ username: active, message });
    this.setState({ message: '' });

    // force a refresh to append the message
    // we could probably do this in the browser but we can be lazy
    this.fetchConversations();
  };

  validInput = () =>
    (this.state.active || '').length > 0 &&
    (this.state.message || '').length > 0;

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
      (previousState) => ({
        active: username,
        loading: true,
        message: previousState.active === username ? previousState.message : '',
      }),
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

            try {
              this.messageRef.current.focus();
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
                          <ChatMessageHistory
                            formatTimestamp={this.formatTimestamp}
                            messages={messages}
                            selfUsername={user.username}
                          />
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
                          onClick: this.sendReply,
                        }}
                        fluid
                        input={
                          <input
                            autoComplete="off"
                            data-lpignore="true"
                            id="chat-message-input"
                            onChange={(event) =>
                              this.setState({ message: event.target.value })
                            }
                            type="text"
                            value={this.state.message}
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
      </div>
    );
  }
}

export default Chat;
