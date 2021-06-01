import React, { useState, useEffect } from 'react';

import {
  Item,
  Segment
} from 'semantic-ui-react';
import User from './User';

import { get } from '../../lib/users';

import './Users.css';

const Users = (props) => {
  const [user, setUser] = useState({});

  useEffect(() => {
    const fetchUser = async () => {
      // const user = await get({ username: 'username' });
      // const [info, status, endpoint] = await Promise.all([
      //   getInfo({ username }),
      //   getStatus({ username }),
      //   getEndpoint({ username })
      // ]);
    
      // console.log(info, status, endpoint)
      // return { ...info.data, ...status.data, ...endpoint.data };
      console.log(user);
      setUser(user);
    }

    fetchUser();
  }, []);

  return (
    <div className='users-container'>
      <Segment className='users-segment' raised>
        <Item.Group>
          <User {...user}/>
        </Item.Group>
      </Segment>
    </div>
  );
};

export default Users;