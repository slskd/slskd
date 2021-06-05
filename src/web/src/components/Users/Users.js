import React, { useState, useEffect, useLayoutEffect, useRef } from 'react';
import {
  Item,
  Segment,
  Input
} from 'semantic-ui-react';

import User from './User';
import { activeUserInfoKey } from '../../config';
import * as peers from '../../lib/peers';

import './Users.css';

const Users = (props) => {
  const inputRef = useRef();
  const [user, setUser] = useState();
  const [usernameInput, setUsernameInput] = useState();
  const [selectedUsername, setSelectedUsername] = useState(undefined);
  const [{ fetching, error }, setStatus] = useState({ fetching: false, error: undefined });

  useEffect(() => {
    document.addEventListener("keyup", keyUp, false);

    const storedUsername = localStorage.getItem(activeUserInfoKey);

    if (storedUsername !== undefined) {
      setSelectedUsername(storedUsername);
      setInputText(storedUsername);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useLayoutEffect(() => {
    document.removeEventListener('keyup', keyUp, false);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const fetchUser = async () => {
      if (!selectedUsername) {
        return;
      }

      setStatus({ fetching: true, error: undefined });

      try {
        const [info, status, endpoint] = await Promise.all([
          peers.getInfo({ username: selectedUsername, bypassCache: true }),
          peers.getStatus({ username: selectedUsername }),
          peers.getEndpoint({ username: selectedUsername })
        ]);
      
        localStorage.setItem(activeUserInfoKey, selectedUsername);
        setUser({ ...info.data, ...status.data, ...endpoint.data });
        setStatus({ fetching: false, error: undefined });
      } catch (error) {
        setStatus({ fetching: false, error: error });
      }
    }

    fetchUser();
  }, [selectedUsername]);

  const clear = () => {
    localStorage.removeItem(activeUserInfoKey);
    setSelectedUsername(undefined);
    setUser(undefined);
    setInputText('');
    setInputFocus();
  }

  const setInputText = (text) => {
    inputRef.current.inputRef.current.value = text;
  }

  const setInputFocus = () => {
    inputRef.current.focus();
  }

  const keyUp = (e) => e.key === 'Escape' ? clear() : '';

  return (
    <div className='users-container'>
      <Segment className='users-selection' raised>
        <Input
          input={<input placeholder="Username" type="search" data-lpignore="true" disabled={!!user || fetching}></input>}
          size='big'
          loading={fetching}
          disabled={fetching}
          ref={inputRef}
          className='users-input'
          placeholder="Username"
          onChange={(e) => setUsernameInput(e.target.value)}
          action={!fetching && (!user ? { icon: 'search', onClick: () => setSelectedUsername(usernameInput) } : { icon: 'x', color: 'red', onClick: clear })}
          onKeyUp={(e) => e.key === 'Enter' ? setSelectedUsername(usernameInput) : ''}
        />
      </Segment>
      {!fetching && !error && !!user && <Segment className='users-user' raised>
        <Item.Group>
          <User {...user}/>
        </Item.Group>
      </Segment>}
    </div>
  );
};

export default Users;