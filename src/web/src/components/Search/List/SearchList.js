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
  const [searches, setSearches] = useState({});
  const inputRef = useRef();

  useEffect(() => {
    get();

    const searchHub = createSearchLogHubConnection();

    searchHub.on('update', search => {
      setConnected(true);
      setSearches(old => ({ ...old, [search.id]: search }))
    });

    searchHub.on('delete', search => {
      setConnected(true);
      setSearches(old => {
        delete old[search.id];
        return { ...old }
      });
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
      setSearches(all.reduce((acc, search) => {
        acc[search.id] = search;
        return acc;
      }, {}));
      setLoading({ loading: false, error: false })
    } catch (error) {
      setLoading({ loading: false, error: error.message })
    }
  }

  const remove = async (search) => {
    console.log('remove', searches)
    try {
      await lib.remove({ id: search.id })
      setSearches(old => {
        delete old[search.id];
        return { ...old }
      });
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
                <Table.HeaderCell className="search-list-icon"></Table.HeaderCell>
                <Table.HeaderCell className="search-list-phrase">Search Phrase</Table.HeaderCell>
                <Table.HeaderCell className="search-list-files">Files</Table.HeaderCell>
                <Table.HeaderCell className="search-list-locked">Locked</Table.HeaderCell>
                <Table.HeaderCell className="search-list-responses">Responses</Table.HeaderCell>
                <Table.HeaderCell className="search-list-started">Started</Table.HeaderCell>
                <Table.HeaderCell className="search-list-action"></Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
            {Object.values(searches)
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