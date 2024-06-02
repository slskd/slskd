import { activeRoomKey } from '../../config';
import * as rooms from '../../lib/rooms';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import RoomMenu from './RoomMenu';
import RoomUserList from './RoomUserList';
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
      },
    );
  }

  componentWillUnmount() {
    const { messages: messagesInterval, rooms: roomsInterval } =
      this.state.intervals;

    clearInterval(roomsInterval);
    clearInterval(messagesInterval);

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
                              <List.Content
                                className={`room-message ${message.self ? 'room-message-self' : ''}`}
                                key={`${message.timestamp}+${message.message}`}
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
      </div>
    );
  }
}

export default Rooms;
