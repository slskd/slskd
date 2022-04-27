import { orderBy } from 'lodash';
import React, { useState, useEffect, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';


import * as lib from '../../lib/searches';
import { createSearchLogHubConnection } from '../../lib/hubFactory';

import './Search.css';

import {
  Input,
  Segment,
  Card,
  Table,
  Icon
} from 'semantic-ui-react';
import SearchIcon from './SearchIcon';

const Searches = () => {
  const [{ loading, loadError }, setLoading] = useState({ loading: true, loadError: false });
  const [{ creating, createError }, setCreating] = useState({ creating: false, createError: false });
  const [connected, setConnected] = useState(false);
  const [searches, setSearches] = useState([]);
  const inputRef = useRef();

  useEffect(() => {
    get();

    const searchHub = createSearchLogHubConnection();

    searchHub.on('update', search => {
      setConnected(true);
      setSearches(old => {
        const idx = old.findIndex(s => s.id === search.id);

        if (idx < 0) {
          return [search, ...old]
        } else {
          old[idx] = search
          return [...old];
        }
      })
    });

    searchHub.on('response', () => { });

    searchHub.onreconnecting(() => setConnected(false));
    searchHub.onclose(() => setConnected(false));
    searchHub.onreconnected(() => setConnected(true));

    searchHub.start();
  }, []);

  const create = async () => {
    const searchText = inputRef.current.inputRef.current.value;
    const id = uuidv4();

    try {
      setCreating({ creating: true, createError: false })
      lib.create({ id, searchText })
      setCreating({ creating: false, createError: false })
      inputRef.current.inputRef.current.value = '';
    } catch (error) {
      setCreating({ creating: false, createError: error.message })
    }
  }

  const get = async () => {
    setLoading({ loading: true, error: false });

    try {
      const all = await lib.getAll();
      setSearches(all);
      setLoading({ loading: false, error: false })
    } catch (error) {
      setLoading({ loading: false, error: error.message })
    }
  }

  const remove = async (search) => {
    console.log('remove', searches)
    try {
      await lib.remove({ id: search.id })
      setSearches(old => old.filter(s => s.id !== search.id))
    } catch (err) {
      console.error(err)
      // noop
    }
  }

  const stop = async (search) => {
    await lib.stop({ id: search.id })
  }

  const cancelAndDeleteAll = () => {
    // todo
  }

  return (
    <div className='search-container'>
        <Segment className='search-segment' raised>
          <Input
            input={<input placeholder="Search phrase" type="search" data-lpignore="true"></input>}
            size='big'
            ref={inputRef}
            loading={creating}
            disabled={creating}
            className='search-input'
            placeholder="Search phrase"
            action={{ icon: 'search', onClick: create }}
            onKeyUp={(e) => e.key === 'Enter' ? create() : ''}
          />
        </Segment>
        <Card className='search-card' raised>
          <Card.Content>
            <Card.Header>
              <Icon name='search'/>
              Searches
              <Icon.Group className='close-button' style={{ marginLeft: 10 }}>
                <Icon 
                  name='trash alternate' 
                  color='red' 
                  link
                  onClick={() => cancelAndDeleteAll()}
                />
                <Icon corner name='asterisk'/>
              </Icon.Group>
              <Icon.Group className='close-button' >
                <Icon 
                  name='stop circle' 
                  color='black' 
                  link
                  onClick={() => cancelAndDeleteAll()}
                />
                <Icon corner name='asterisk'/>
              </Icon.Group>
            </Card.Header>
            <Table selectable>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell></Table.HeaderCell>
                  <Table.HeaderCell>Started</Table.HeaderCell>
                  <Table.HeaderCell>Search Phrase</Table.HeaderCell>
                  <Table.HeaderCell>Responses</Table.HeaderCell>
                  <Table.HeaderCell>Files (Locked)</Table.HeaderCell>
                  <Table.HeaderCell></Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
              {searches
                .sort((a, b) => {
                  //console.log(new Date(b.startedAt), new Date(a.startedAt))
                  return new Date(b.startedAt) - new Date(a.startedAt)
                })
                .map((s, index) => <Table.Row style={{ cursor: 'pointer' }} key={index}>
                  <Table.Cell><SearchIcon state={s.state} alt={s.state}/></Table.Cell>
                  <Table.Cell>{s.startedAt}</Table.Cell>
                  <Table.Cell>{s.searchText}</Table.Cell>
                  <Table.Cell>{s.responseCount}</Table.Cell>
                  <Table.Cell>{s.fileCount} ({s.lockedFileCount})</Table.Cell>
                  <Table.Cell>{s.state.includes('Completed') ? 
                    <Icon name="trash alternate" color='red' onClick={() => remove(s)}/> : 
                    <Icon name="stop circle" color="red" onClick={() => stop(s)}/>}
                  </Table.Cell>
                </Table.Row>)}
              </Table.Body>
            </Table>
          </Card.Content>
      </Card>
    </div>
  )
};

export default Searches;