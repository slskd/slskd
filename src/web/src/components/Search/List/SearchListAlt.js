import React, { useState, useEffect, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';


import * as lib from '../../../lib/searches';
import { createSearchLogHubConnection } from '../../../lib/hubFactory';

import SearchListRow from './SearchListRow';

import {
  Card,
  Item,
  Icon,
  Button,
  Label,
  Table,
  Statistic
} from 'semantic-ui-react';
import SearchIcon from '../SearchIcon';

const SearchListAlt = () => {
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
        <Item.Group divided>
          {searches
            .sort((a, b) => (new Date(b.startedAt) - new Date(a.startedAt)))
            .map((search, index) => 
              <Item>
                {/* <Item.Image src='https://react.semantic-ui.com/images/wireframe/image.png' /> */}
                <Item.Content>
                  <Item.Header>
                    {search.searchText}
                  </Item.Header>
                  <Item.Meta>Basic Search | {search.state}</Item.Meta>
                  <Item.Description>
                  </Item.Description>
                  <Item.Extra>
                  <Statistic.Group widths='4' style={{marginTop: 30}} size='mini'>
                      <Statistic>
                        <Statistic.Value>{search.responseCount}</Statistic.Value>
                        <Statistic.Label>Responses</Statistic.Label>
                      </Statistic>
                      <Statistic>
                        <Statistic.Value>{search.fileCount}</Statistic.Value>
                        <Statistic.Label>Files</Statistic.Label>
                      </Statistic>
                      <Statistic color='yellow'>
                        <Statistic.Value><Icon name='lock'/>{search.lockedFileCount}</Statistic.Value>
                        <Statistic.Label>
                          Locked
                        </Statistic.Label>
                      </Statistic>
                    </Statistic.Group>
                    <Icon size='large' style={{float: 'right', marginTop: -50}} name='trash alternate' color='red'/>
                  </Item.Extra>
                </Item.Content>
              </Item>
            )
          }
        </Item.Group>
      </Card.Content>
    </Card>
  )
};

export default SearchListAlt;