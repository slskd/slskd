import './Users.css';
import { activeUserInfoKey } from '../../config';
import * as users from '../../lib/users';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import User from './User';
import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Icon, Input, Item, Loader, Segment } from 'semantic-ui-react';

const Users = (props) => {
  const inputRef = useRef();
  const [user, setUser] = useState();
  const [usernameInput, setUsernameInput] = useState();
  const [selectedUsername, setSelectedUsername] = useState(undefined);
  const [{ error, fetching }, setStatus] = useState({
    error: undefined,
    fetching: false,
  });

  useEffect(() => {
    document.addEventListener('keyup', keyUp, false);

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

      setStatus({ error: undefined, fetching: true });

      try {
        const [info, status, endpoint] = await Promise.all([
          users.getInfo({ username: selectedUsername }),
          users.getStatus({ username: selectedUsername }),
          users.getEndpoint({ username: selectedUsername }),
        ]);

        localStorage.setItem(activeUserInfoKey, selectedUsername);
        setUser({ ...info.data, ...status.data, ...endpoint.data });
        setStatus({ error: undefined, fetching: false });
      } catch (error) {
        setStatus({ error, fetching: false });
      }
    };

    fetchUser();
  }, [selectedUsername]);

  const clear = () => {
    localStorage.removeItem(activeUserInfoKey);
    setSelectedUsername(undefined);
    setUser(undefined);
    setInputText('');
    setInputFocus();
  };

  const setInputText = (text) => {
    inputRef.current.inputRef.current.value = text;
  };

  const setInputFocus = () => {
    inputRef.current.focus();
  };

  const keyUp = (e) => (e.key === 'Escape' ? clear() : '');

  return (
    <div className="users-container">
      <Segment
        className="users-segment"
        raised
      >
        <div className="users-segment-icon">
          <Icon
            name="users"
            size="big"
          />
        </div>
        <Input
          action={
            !fetching &&
            (!user
              ? {
                  icon: 'search',
                  onClick: () => setSelectedUsername(usernameInput),
                }
              : { color: 'red', icon: 'x', onClick: clear })
          }
          className="users-input"
          disabled={fetching}
          input={
            <input
              data-lpignore="true"
              disabled={Boolean(user) || fetching}
              placeholder="Username"
              type="search"
            />
          }
          loading={fetching}
          onChange={(e) => setUsernameInput(e.target.value)}
          onKeyUp={(e) =>
            e.key === 'Enter' ? setSelectedUsername(usernameInput) : ''
          }
          placeholder="Username"
          ref={inputRef}
          size="big"
        />
      </Segment>
      {fetching ? (
        <Loader
          active
          className="search-loader"
          inline="centered"
          size="big"
        />
      ) : (
        <div>
          {error ? (
            <span>Failed to retrieve information for {selectedUsername}</span>
          ) : !user ? (
            <PlaceholderSegment
              caption="No user info to display"
              icon="users"
            />
          ) : (
            <Segment
              className="users-user"
              raised
            >
              <Item.Group>
                <User {...user} />
              </Item.Group>
            </Segment>
          )}
        </div>
      )}
    </div>
  );
};

export default Users;
