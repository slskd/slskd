import React, { useEffect, useState, useMemo } from 'react';
import * as rooms from '../../lib/rooms';
import './Rooms.css';

import {
  Icon, Button, Modal, Table, Header, Input, Loader, Dimmer,
} from 'semantic-ui-react';

const RoomJoinModal = ({ joinRoom: parentJoinRoom, ...modalOptions }) => {
  const [open, setOpen] = useState(false);
  const [available, setAvailable] = useState([]);
  const [selected, setSelected] = useState(undefined);
  const [sortBy, setSortBy] = useState('name');
  const [sortOrder, setSortOrder] = useState('desc');
  const [filter, setFilter] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const getAvailableRooms = async () => {
      setLoading(true);
      const available = await rooms.getAvailable();
      setAvailable(available);
      setLoading(false);
    };

    if (open) getAvailableRooms();
  }, [open]);

  const sortedAvailable = useMemo(() => {
    const sorted = [...available].filter(room => room.name.includes(filter));

    sorted.sort((a, b) => {
      if (sortOrder === 'asc') {
        if (typeof(a[sortBy]) === 'string') {
          return b[sortBy].localeCompare(a[sortBy]);
        }

        return a[sortBy] - b[sortBy];
      }

      if (typeof(a[sortBy]) === 'string') {
        return a[sortBy].localeCompare(b[sortBy]);
      }

      return b[sortBy] - a[sortBy];
    });
    
    return sorted;
  }, [available, sortBy, sortOrder, filter]);

  const joinRoom = async () => {
    await parentJoinRoom(selected);
    close();
  };

  const isSelected = (room) => selected === room.name;

  const close = () => {
    setAvailable([]);
    setSelected(undefined);
    setSortBy('name');
    setSortOrder('desc');
    setFilter('');
    setOpen(false);
  };

  return (
    <Modal
      className='join-room-modal'
      open={open}
      onClose={() => close()}
      onOpen={() => setOpen(true)}
      {...modalOptions}
    >
      <Header>
        <Icon name='comments'/>
        <Modal.Content>Join Room</Modal.Content>
      </Header>
      <Modal.Content scrolling>
        {loading ? <Dimmer active inverted><Loader inverted content='Loading Room List' /></Dimmer> : 
          <>
            <Input 
              fluid 
              onChange={(_, e) => setFilter(e.value)}
              placeholder='Room Filter'
              icon='filter'
            />
            <Table celled selectable>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell onClick={() => setSortBy('name')}>
                  Name
                    <Icon 
                      link={sortBy === 'name'}
                      name={sortBy === 'name' && (sortOrder === 'asc' ? 'chevron up' : 'chevron down')}
                      onClick={() => setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc')}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell onClick={() => setSortBy('userCount')}>
                  Users
                    <Icon 
                      link={sortBy === 'userCount'}
                      name={sortBy === 'userCount' && (sortOrder === 'asc' ? 'chevron up' : 'chevron down')}
                      onClick={() => setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc')}
                    />
                  </Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {sortedAvailable.map((room, index) => 
                  <Table.Row
                    key={index}
                    style={isSelected(room) ? {fontWeight: 'bold'} : {}}
                    onClick={() => setSelected(room.name)}
                  >
                    <Table.Cell>
                      {isSelected(room) && <Icon name='check' color='green'/>}
                      {room.isPrivate && <Icon name='lock'/>}
                      {room.isOwned && <Icon name='chess queen'/>}
                      {room.isModerated && <Icon name='gavel'/>}
                      {room.name}
                    </Table.Cell>
                    <Table.Cell>{room.userCount}</Table.Cell>
                  </Table.Row>
                )}
              </Table.Body>
            </Table>
          </>}
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={() => setOpen(false)}>Cancel</Button>
        <Button 
          positive
          onClick={() => joinRoom()}
          disabled={!selected}
        >Join</Button>
      </Modal.Actions>
    </Modal>
  );
};

export default RoomJoinModal;