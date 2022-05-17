import React, { useState, useEffect, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';
import { useParams, useHistory, useRouteMatch } from 'react-router-dom';

import * as lib from '../../lib/searches';
import { createSearchHubConnection } from '../../lib/hubFactory';

import LoaderSegment from '../Shared/LoaderSegment';
import SearchList from './List/SearchList';

import './Search.css';

import {
  Input,
  Segment,
  Button,
  Loader,
} from 'semantic-ui-react';

import SearchDetail from './Detail/SearchDetail';
import ErrorSegment from '../Shared/ErrorSegment';

const Searches = () => {
  const [{ connected, connecting, connectError } , setConnected] = useState({ connected: false, connecting: true, connectError: false });
  const [{ creating, createError }, setCreating] = useState({ creating: false, createError: false });
  const [error, setError] = useState(undefined);
  const [searches, setSearches] = useState({});
  const inputRef = useRef();
  const searchRef = useRef();

  const { id: searchId } = useParams();
  const history = useHistory();
  const match = useRouteMatch();

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
      console.log('list of searches received', searches);
      onUpdate(searches.reduce((acc, search) => {
        acc[search.id] = search;
        return acc;
      }, {}));
      onConnected();
    })

    searchHub.on('update', search => {
      console.log(search.responses)
      onUpdate(old => ({ ...old, [search.id]: search }));
    });

    searchHub.on('delete', search => {
      onUpdate(old => {
        delete old[search.id];
        return { ...old }
      });
    });

    searchHub.on('create', () => {});

    searchHub.onreconnecting((error) => onConnectionError(error?.message ?? 'Disconnected'));
    searchHub.onreconnected(() => onConnected());
    searchHub.onclose((error) => onConnectionError(error?.message ?? 'Disconnected'));

    const connect = async () => {
      try {
        console.log('connecting to search hub');
        onConnecting();
        await searchHub.start();
        console.log('search hub connected');
      } catch (error) {
        onConnectionError(error?.message ?? 'Failed to connect')
      }
    }

    connect();

    return () => {
      console.log('stopping search hub');
      searchHub.stop();
      console.log('search hub stopped');
    }
  }, []);

  const create = async ({ search, navigate = false } = {}) => {
    const searchText = search || inputRef.current.inputRef.current.value;
    const id = uuidv4();
    
    try {
      setCreating({ creating: true, createError: false })
      await lib.create({ id, searchText })
      setCreating({ creating: false, createError: false })

      try {
        inputRef.current.inputRef.current.value = '';
      } catch {
        // no-op
      }

      if (navigate) {
        history.push(`/searches/${id}`)
      }
    } catch (error) {
      console.error(error)
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
      setError(error?.message ?? error);
    }
  };

  const stop = async (search) => {
    await lib.stop({ id: search.id })
  }

  if (connecting) {
    return <LoaderSegment/>
  }

  if (connectError) {
    return <ErrorSegment caption={connectError}/>;
  }

  if (searchId) {
    if (searches[searchId]) {
      return (
        <SearchDetail
          search={searches[searchId]}
          onCreate={create}
          onStop={stop}
          onRemove={remove}
        />
      );
    }

    return (
      <ErrorSegment caption='Invalid Search ID'/>
    );
  }

  return (
    <>
      <Segment className='search-segment' raised>
        <Input
          input={<input placeholder="Search phrase" type="search" data-lpignore="true"></input>}
          size='big'
          ref={inputRef}
          loading={creating}
          disabled={creating}
          className='search-input'
          placeholder="Search phrase"
          action={<>
            <Button icon='plus' onClick={create}/>
            <Button icon='search' onClick={() => create({ navigate: true })}/>
          </>}
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
    </>
  )
};

export default Searches;