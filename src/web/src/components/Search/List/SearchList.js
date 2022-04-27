import React, { useState, useEffect, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';


import * as lib from '../../../lib/searches';
import { createSearchLogHubConnection } from '../../../lib/hubFactory';

import SearchListRow from './SearchListRow';

import {
  Card,
  Table,
  Icon
} from 'semantic-ui-react';
import SearchIcon from '../SearchIcon';

const SearchList = () => {
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
    <>
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
          <Table size='large' selectable>
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
              .sort((a, b) => (new Date(b.startedAt) - new Date(a.startedAt)))
              .map((search, index) => <SearchListRow
                search={search}
                key={index}
                onRemove={remove}
                onStop={stop}
              />)}
            </Table.Body>
          </Table>
        </Card.Content>
      </Card>
    </>
  )
};

export default SearchList;