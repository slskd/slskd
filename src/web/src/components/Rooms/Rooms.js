import React, { Component, createRef } from 'react';
import * as rooms from '../../lib/rooms';
import { activeRoomKey } from '../../config';

import { Segment, Card, Icon, Input, Ref, List, Loader, Dimmer } from 'semantic-ui-react';

import RoomMenu from './RoomMenu';
import RoomUserList from './RoomUserList';
import PlaceholderSegment from '../Shared/PlaceholderSegment';

const initialState = {
  active: '',
  joined: [],
  room: {
    messages: [],
    users: [],
  },
  intervals: {
    rooms: undefined,
    messages: undefined,
  },
  loading: false,
};

class Rooms extends Component {
  state = initialState;
  messageRef = undefined;
  listRef = createRef();

  componentDidMount = async () => {
    this.setState({ 
      intervals: {
        rooms: window.setInterval(this.fetchJoinedRooms, 500),
        messages: window.setInterval(this.fetchActiveRoom, 1000),
      },
      active: sessionStorage.getItem(activeRoomKey) || '',
    }, async () => {
      await this.fetchJoinedRooms();
      this.selectRoom(this.state.active || this.getFirstRoom());
    });
  };

  componentWillUnmount = () => {
    const { rooms, messages } = this.state.intervals;

    clearInterval(rooms);
    clearInterval(messages);

    this.setState({ intervals: initialState.intervals });
  };

  getFirstRoom = () => {
    return this.state.joined.length > 0 ? this.state.joined[0] : '';
  };

  fetchJoinedRooms = async () => {
    const joined = await rooms.getJoined();
    this.setState({
      joined,
    }, () => {
      if (!this.state.joined.includes(this.state.active)) {
        this.selectRoom(this.getFirstRoom());
      }
    });
  };

  fetchActiveRoom = async () => {
    const { active } = this.state;

    if (active.length === 0) return;

    const messages = await rooms.getMessages({ roomName: active });
    const users = await rooms.getUsers({ roomName: active });

    this.setState({
      room: {
        users,
        messages,
      },
    });
  };

  selectRoom = async (roomName) => {
    this.setState({ 
      active: roomName, 
      room: initialState.room,
      loading: true,
    }, async () => {
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
    });
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
    (this.state.active || '').length > 0
    && ((this.messageRef && this.messageRef.current && this.messageRef.current.value) || '').length > 0;
  
  focusInput = () => {
    this.messageRef.current.focus();
  };

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    const dtfUS = new Intl.DateTimeFormat('en', { 
      month: 'numeric', 
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });

    return dtfUS.format(date);
  };

  sendMessage = async () => {
    const { active } = this.state;
    const message = this.messageRef.current.value;

    if (!this.validInput()) {
      return;
    }

    await rooms.sendMessage({ roomName: active, message });
    this.messageRef.current.value = '';
  };

  render = () => {
    const { joined = [], active = [], room, loading } = this.state;

    return (
      <div className='rooms'>
        <Segment className='rooms-segment' raised>
          <div className="rooms-segment-icon"><Icon name="comments" size="big"/></div>
          <RoomMenu
            joined={joined}
            active={active}
            onRoomChange={(name) => this.selectRoom(name)}
            joinRoom={this.joinRoom}
          />
        </Segment>
        {!active ? 
          <PlaceholderSegment icon='comments' caption='No rooms to display'/> :
          <Card className='room-active-card' raised>
            <Card.Content onClick={() => this.focusInput()}>
              <Card.Header>
                <Icon name='circle' color='green'/>
                {active}
                <Icon 
                  className='close-button' 
                  name='close' 
                  color='red' 
                  link
                  onClick={() => this.leaveRoom(active)}
                />
              </Card.Header>
              <div className='room'>
                {loading ? <Dimmer active inverted><Loader inverted/></Dimmer> : <>
                  <Segment.Group>
                    <Segment className='room-history'>
                      <Ref innerRef={this.listRef}>
                        <List>
                          {room.messages.map((message, index) =>
                            <List.Content
                              key={index}
                              className={`room-message ${message.self ? 'room-message-self' : ''}`}
                            >
                              <span className='room-message-time'>{this.formatTimestamp(message.timestamp)}</span>
                              <span className='room-message-name'>{message.username}: </span>
                              <span className='room-message-message'>{message.message}</span>
                            </List.Content>
                          )}
                          <List.Content id='room-history-scroll-anchor'/>
                        </List>
                      </Ref>
                    </Segment>
                    <Segment className='room-input'>
                      <Input
                        fluid
                        transparent
                        input={
                          <input id='room-message-input' type="text" data-lpignore="true" autoComplete="off"></input>}
                        ref={input => this.messageRef = input && input.inputRef}
                        action={{
                          icon: <Icon name='send' color='green'/>,
                          className: 'room-message-button', onClick: this.sendMessage,
                          disabled: !this.validInput(),
                        }}
                        onKeyUp={(e) => e.key === 'Enter' ? this.sendMessage() : ''}
                      />
                    </Segment>
                  </Segment.Group>
                  <Segment className='room-users'>
                    <RoomUserList users={room.users}/>
                  </Segment>
                </>}
              </div>
            </Card.Content>
          </Card>}
      </div>
    );
  };
}

export default Rooms;