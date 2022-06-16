import React, { useState, useEffect, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';
import { useParams, useHistory, useRouteMatch } from 'react-router-dom';
import { toast } from 'react-toastify';

import * as lib from '../../lib/searches';
import { createSearchHubConnection } from '../../lib/hubFactory';

import LoaderSegment from '../Shared/LoaderSegment';
import SearchList from './List/SearchList';

import './Search.css';

import {
  Input,
  Segment,
  Button,
  Icon,
} from 'semantic-ui-react';

import SearchDetail from './Detail/SearchDetail';
import ErrorSegment from '../Shared/ErrorSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';

const Searches = ({ server }) => {
  const [connecting, setConnecting] = useState(true);
  const [error, setError] = useState(undefined);
  const [searches, setSearches] = useState({});

  const [removing, setRemoving] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [creating, setCreating] = useState(false);

  const inputRef = useRef();

  const { id: searchId } = useParams();
  const history = useHistory();
  const match = useRouteMatch();

  const onConnecting = () => { setConnecting(true); };
  const onConnected = () => { setConnecting(false); setError(undefined); };
  const onConnectionError = (error) => { setConnecting(false); setError(error); };

  const onUpdate = (update) => {
    onConnected();
    setSearches(update);
  };

  useEffect(() => {
    onConnecting();
    
    const searchHub = createSearchHubConnection();

    searchHub.on('list', searches => {
      onUpdate(searches.reduce((acc, search) => {
        acc[search.id] = search;
        return acc;
      }, {}));
      onConnected();
    });

    searchHub.on('update', search => {
      onUpdate(old => ({ ...old, [search.id]: search }));
    });

    searchHub.on('delete', search => {
      onUpdate(old => {
        delete old[search.id];
        return { ...old };
      });
    });

    searchHub.on('create', () => {});

    searchHub.onreconnecting((error) => onConnectionError(error?.message ?? 'Disconnected'));
    searchHub.onreconnected(() => onConnected());
    searchHub.onclose((error) => onConnectionError(error?.message ?? 'Disconnected'));

    const connect = async () => {
      try {
        onConnecting();
        await searchHub.start();
      } catch (error) {
        toast.error(error?.message ?? 'Failed to connect');
        onConnectionError(error?.message ?? 'Failed to connect');
      }
    };

    connect();

    return () => {
      searchHub.stop();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // create a new search, and optionally navigate to it to display the details
  // we do this if the user clicks the search icon, or repeats an existing search
  const create = async ({ search, navigate = false } = {}) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = search || ref.value;
    const id = uuidv4();
    
    try {
      setCreating(true);
      await lib.create({ id, searchText });
      
      try {
        ref.value = '';
      } catch {
        // we are probably repeating an existing search; the input isn't mounted.  no-op.
      }
      
      setCreating(false);

      if (navigate) {
        history.push(`${match.url.replace(`/${searchId}`, '')}/${id}`);
      }
    } catch (error) {
      console.error(error);      
      toast.error(error?.response?.data ?? error?.message ?? error);
      setCreating(false);
    }
  };

  // delete a search
  const remove = async (search) => {
    try {
      setRemoving(true);

      await lib.remove({ id: search.id });
      setSearches(old => {
        delete old[search.id];
        return { ...old };
      });

      setRemoving(false);
    } catch (err) {
      console.error(err);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRemoving(false);
    }
  };

  // stop an in-progress search
  const stop = async (search) => {
    try {
      setStopping(true);
      await lib.stop({ id: search.id });
      setStopping(false);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setStopping(false);
    }
  };

  if (connecting) {
    return <LoaderSegment/>;
  }

  if (error) {
    return <ErrorSegment caption={error?.message ?? error}/>;
  }

  // if searchId is not null, there's an id in the route.
  // display the details for the search, if there is one
  if (searchId) {
    if (searches[searchId]) {
      return (
        <SearchDetail
          search={searches[searchId]}
          creating={creating}
          stopping={stopping}
          removing={removing}
          disabled={!server.isConnected}
          onCreate={create}
          onStop={stop}
          onRemove={remove}
        />
      );
    }

    // if the searchId doesn't match a search we know about, chop
    // the id off of the url and force navigation back to the list
    history.replace(match.url.replace(`/${searchId}`, ''));
  }

  return (
    <>
      <Segment className='search-segment' raised>
        <div className="search-segment-icon"><Icon name="search" size="big"/></div>
        <Input
          input={
            <input
              placeholder={server.isConnected ? 'Search phrase' : 'Connect to server to perform a search'}
              type="search"
              data-lpignore="true"
            ></input>}
          size='big'
          ref={inputRef}
          loading={creating}
          disabled={creating || !server.isConnected}
          className='search-input'
          placeholder="Search phrase"
          action={<>
            <Button icon='plus' disabled={creating || !server.isConnected} onClick={create}/>
            <Button
              icon='search'
              disabled={creating || !server.isConnected}
              onClick={() => create({ navigate: true })}
            />
          </>}
          onKeyUp={(e) => e.key === 'Enter' ? create() : ''}
        />
      </Segment>
      {Object.keys(searches).length === 0 
        ? <PlaceholderSegment icon="search" caption="No searches to display"/>
        : <SearchList
          connecting={connecting}
          error={error}
          searches={searches}
          onRemove={remove}
          onStop={stop}
        />
      }
    </>
  );
};

export default Searches;