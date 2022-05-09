import React, { useState, useEffect, useRef } from 'react';
import {
  BrowserRouter as Router,
  Switch,
  Route,
  Link,
  useParams
} from "react-router-dom";
import { v4 as uuidv4 } from 'uuid';


import * as lib from '../../lib/searches';
import { createSearchHubConnection } from '../../lib/hubFactory';

import SearchList from './List/SearchList';

import './Search.css';

import {
  Input,
  Segment,
} from 'semantic-ui-react';

const Searches = () => {
  const [{ connected, connecting, connectError } , setConnected] = useState({ connected: false, connecting: true, connectError: false });
  const [{ creating, createError }, setCreating] = useState({ creating: false, createError: false });
  const [error, setError] = useState(undefined);
  const [searches, setSearches] = useState({});
  const params = useParams();
  const inputRef = useRef();

  const onConnecting = () => setConnected({ connected: false, connecting: true, connectError: false })
  const onConnected = () => setConnected({ connected: true, connecting: false, connectError: false });
  const onConnectionError = (error) => setConnected({ connected: false, connecting: false, connectError: error })

  const onUpdate = (update) => {
    onConnected();
    setSearches(update);
  }

  useEffect(() => {
    onConnecting();
    
    const searchHub = createSearchHubConnection();

    searchHub.on('list', searches => {
      onUpdate(searches.reduce((acc, search) => {
        acc[search.id] = search;
        return acc;
      }, {}));
      onConnected();
    })

    searchHub.on('update', search => {
      onUpdate(old => ({ ...old, [search.id]: search }));
    });

    searchHub.on('delete', search => {
      onUpdate(old => {
        delete old[search.id];
        return { ...old }
      });
    });

    searchHub.onreconnecting((error) => onConnectionError(error?.message ?? 'Disconnected'));
    searchHub.onreconnected(() => onConnected());
    searchHub.onclose((error) => onConnectionError(error?.message ?? 'Disconnected'));

    const connect = async () => {
      try {
        onConnecting();
        await searchHub.start();
      } catch (error) {
        onConnectionError(error?.message ?? 'Failed to connect')
      }
    }

    connect();

    return () => {
      searchHub.stop();
    }
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

  const remove = async (search) => {
    try {
      await lib.remove({ id: search.id })
      setSearches(old => {
        delete old[search.id];
        return { ...old }
      });
    } catch (err) {
      setError(error.message);
    }
  };

  const stop = async (search) => {
    await lib.stop({ id: search.id })
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
      <SearchList
        connecting={connecting}
        error={error}
        searches={searches}
        onRemove={remove}
        onStop={stop}
      />
    </div>
  )
};

export default Searches;