import { activeRoomKey } from '../../config';
import * as optionsApi from '../../lib/options';
import * as rooms from '../../lib/rooms';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import RoomMenu from './RoomMenu';
import RoomUserList from './RoomUserList';
import React, { Component, createRef } from 'react';
import { withRouter } from 'react-router-dom';
import { toast } from 'react-toastify';
import {
  Button,
  Card,
  Dimmer,
  Icon,
  Input,
  List,
  Loader,
  Portal,
  Ref,
  Segment,
} from 'semantic-ui-react';
import YAML from 'yaml';

const initialState = {
  active: '',
  contextMenu: {
    message: null,
    open: false,
    x: 0,
    y: 0,
  },
  intervals: {
    messages: undefined,
    rooms: undefined,
  },
  joined: [],
  loading: false,
  room: {
    messages: [],
    users: [],
  },
};

class Rooms extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    this.setState(
      {
        active: sessionStorage.getItem(activeRoomKey) || '',
        intervals: {
          messages: window.setInterval(this.fetchActiveRoom, 1_000),
          rooms: window.setInterval(this.fetchJoinedRooms, 500),
        },
      },
      async () => {
        await this.fetchJoinedRooms();
        this.selectRoom(this.state.active || this.getFirstRoom());
        document.addEventListener('click', this.handleCloseContextMenu);
      },
    );
  }

  componentWillUnmount() {
    const { messages: messagesInterval, rooms: roomsInterval } =
      this.state.intervals;

    clearInterval(roomsInterval);
    clearInterval(messagesInterval);

    document.removeEventListener('click', this.handleCloseContextMenu);

    this.setState({ intervals: initialState.intervals });
  }

  listRef = createRef();

  messageRef = undefined;

  getFirstRoom = () => {
    return this.state.joined.length > 0 ? this.state.joined[0] : '';
  };

  fetchJoinedRooms = async () => {
    const joined = await rooms.getJoined();
    this.setState(
      {
        joined,
      },
      () => {
        if (!this.state.joined.includes(this.state.active)) {
          this.selectRoom(this.getFirstRoom());
        }
      },
    );
  };

  fetchActiveRoom = async () => {
    const { active } = this.state;

    if (active.length === 0) return;

    const messages = await rooms.getMessages({ roomName: active });
    const users = await rooms.getUsers({ roomName: active });

    this.setState({
      room: {
        messages,
        users,
      },
    });
  };

  selectRoom = async (roomName) => {
    this.setState(
      {
        active: roomName,
        loading: true,
        room: initialState.room,
      },
      async () => {
        const { active } = this.state;

        sessionStorage.setItem(activeRoomKey, active);

        await this.fetchActiveRoom();
        this.setState({ loading: false }, () => {
          try {
            this.listRef.current.lastChild.scrollIntoView();
          } catch {
            // no-op
          }
        });
      },
    );
  };

  joinRoom = async (roomName) => {
    await rooms.join({ roomName });
    await this.fetchJoinedRooms();
    this.selectRoom(roomName);
  };

  leaveRoom = async (roomName) => {
    await rooms.leave({ roomName });
    await this.fetchJoinedRooms();
    this.selectRoom(this.getFirstRoom());
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

  sendMessage = async () => {
    const { active } = this.state;
    const message = this.messageRef.current.value;

    if (!this.validInput()) {
      return;
    }

    await rooms.sendMessage({ message, roomName: active });
    this.messageRef.current.value = '';
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
    this.messageRef.current.value = `[${this.state.contextMenu.message.username}] ${this.state.contextMenu.message.message} --> `;
    this.focusInput();
  };

  handleUserProfile = () => {
    this.props.history.push('/users', {
      user: this.state.contextMenu.message.username,
    });
  };

  handleBrowseShares = () => {
    this.props.history.push('/browse', {
      user: this.state.contextMenu.message.username,
    });
  };

  handleIgnoreUser = async () => {
    const username = this.state.contextMenu?.message?.username;
    if (!username) return;

    try {
      const yamlText = await optionsApi.getYaml();
      const yamlDocument = YAML.parseDocument(yamlText);

      let groups = yamlDocument.get('groups');

      if (!groups) {
        groups = yamlDocument.createNode({});
        yamlDocument.set('groups', groups);
      }

      let blacklisted = groups.get('blacklisted');

      if (!blacklisted) {
        blacklisted = yamlDocument.createNode({});
        groups.set('blacklisted', blacklisted);
      }

      let members = blacklisted.get('members');

      if (!members) {
        members = yamlDocument.createNode([]);
        blacklisted.set('members', members);
      }

      if (!members.items.includes(username)) {
        members.add(username);
      }

      const newYamlText = yamlDocument.toString();
      await optionsApi.updateYaml({ yaml: newYamlText });
      toast.success(`User '${username}' added to blacklist.`);
    } catch (error) {
      toast.error('Failed to ignore user: ' + error);
    }

    this.handleCloseContextMenu();
  };

  renderContextMenu() {
    const { contextMenu } = this.state;
    return (
      <Portal open={contextMenu.open}>
        <div
          className="ui vertical buttons popup-menu"
          style={{
            left: contextMenu.x,
            maxHeight: `calc(100vh - ${contextMenu.y}px)`,
            top: contextMenu.y,
          }}
        >
          <Button
            className="ui compact button popup-option"
            onClick={this.handleReply}
          >
            Reply
          </Button>
          <Button
            className="ui compact button popup-option"
            onClick={this.handleUserProfile}
          >
            User Profile
          </Button>
          <Button
            className="ui compact button popup-option"
            onClick={this.handleBrowseShares}
          >
            Browse Shares
          </Button>
          <Button
            className="ui compact button popup-option"
            onClick={this.handleIgnoreUser}
          >
            Ignore User
          </Button>
        </div>
      </Portal>
    );
  }

  render() {
    const { active = [], joined = [], loading, room } = this.state;

    return (
      <div className="rooms">
        <Segment
          className="rooms-segment"
          raised
        >
          <div className="rooms-segment-icon">
            <Icon
              name="comments"
              size="big"
            />
          </div>
          <RoomMenu
            active={active}
            joinRoom={this.joinRoom}
            joined={joined}
            onRoomChange={(name) => this.selectRoom(name)}
          />
        </Segment>
        {active?.length === 0 ? (
          <PlaceholderSegment
            caption="No rooms to display"
            icon="comments"
          />
        ) : (
          <Card
            className="room-active-card"
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
                  onClick={() => this.leaveRoom(active)}
                />
              </Card.Header>
              <div className="room">
                {loading ? (
                  <Dimmer
                    active
                    inverted
                  >
                    <Loader inverted />
                  </Dimmer>
                ) : (
                  <>
                    <Segment.Group>
                      <Segment className="room-history">
                        <Ref innerRef={this.listRef}>
                          <List>
                            {room.messages.map((message) => (
                              <div
                                key={`${message.timestamp}+${message.message}`}
                                onContextMenu={(clickEvent) =>
                                  this.handleContextMenu(clickEvent, message)
                                }
                              >
                                <List.Content
                                  className={`room-message ${message.self ? 'room-message-self' : ''}`}
                                >
                                  <span className="room-message-time">
                                    {this.formatTimestamp(message.timestamp)}
                                  </span>
                                  <span className="room-message-name">
                                    {message.username}:{' '}
                                  </span>
                                  <span className="room-message-message">
                                    {message.message}
                                  </span>
                                </List.Content>
                              </div>
                            ))}
                            <List.Content id="room-history-scroll-anchor" />
                          </List>
                        </Ref>
                      </Segment>
                      <Segment className="room-input">
                        <Input
                          action={{
                            className: 'room-message-button',
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
                              id="room-message-input"
                              type="text"
                            />
                          }
                          onKeyUp={(event) =>
                            event.key === 'Enter' ? this.sendMessage() : ''
                          }
                          ref={(input) =>
                            (this.messageRef = input && input.inputRef)
                          }
                          transparent
                        />
                      </Segment>
                    </Segment.Group>
                    <Segment className="room-users">
                      <RoomUserList users={room.users} />
                    </Segment>
                  </>
                )}
              </div>
            </Card.Content>
          </Card>
        )}
        {this.renderContextMenu()}
      </div>
    );
  }
}

export default withRouter(Rooms);
