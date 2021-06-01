import React, { useState, useEffect } from 'react';

import {
  Item,
  Segment,
  Input
} from 'semantic-ui-react';
import User from './User';

import { getInfo, getStatus, getEndpoint } from '../../lib/users';

import './Users.css';

const Users = (props) => {
  const [user, setUser] = useState();
  const [usernameInput, setUsernameInput] = useState();
  const [selectedUsername, setSelectedUsername] = useState(undefined);
  const [{ fetching, error }, setStatus] = useState({ fetching: false, error: undefined });


  useEffect(() => {
    const fetchUser = async () => {
      if (selectedUsername === undefined) {
        return;
      }

      setStatus({ fetching: true, error: undefined });

      try {
        const [info, status, endpoint] = await Promise.all([
          getInfo({ username: selectedUsername }),
          getStatus({ username: selectedUsername }),
          getEndpoint({ username: selectedUsername })
        ]);
      
        setUser({ ...info.data, ...status.data, ...endpoint.data });
        setStatus({ fetching: false, error: undefined });
      } catch (error) {
        setStatus({ fetching: false, error: error });
      }
    }

    fetchUser();
  }, [selectedUsername]);

  const clear = () => {
    setSelectedUsername(undefined);
    setUser(undefined);
  }

  return (
    <div className='users-container'>
      <Segment className='users-selection' raised>
        <Input
            input={<input placeholder="Username" type="search" data-lpignore="true" disabled={!!user || fetching}></input>}
            size='big'
            loading={fetching}
            disabled={fetching}
            className='users-input'
            placeholder="Username"
            onChange={(e) => setUsernameInput(e.target.value)}
            action={!fetching && (!user ? { icon: 'search', onClick: () => setSelectedUsername(usernameInput) } : { icon: 'x', color: 'red', onClick: clear })}
            onKeyUp={(e) => e.key === 'Enter' ? setSelectedUsername() : ''}
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