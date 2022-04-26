import React, { useState, useEffect, useRef } from 'react';
import { v4 as uuidv4 } from 'uuid';

import * as search from '../../lib/search';

import './Search.css';

import {
  Input,
  Segment,
  Card,
  Table,
  Icon
} from 'semantic-ui-react';
import SearchIcon from './SearchIcon';

const Searches = () => {
  const [{ loading, loadError }, setLoading] = useState({ loading: true, loadError: false });
  const [{ creating, createError }, setCreating] = useState({ creating: false, createError: false })
  const [searches, setSearches] = useState([]);
  const inputRef = useRef();

  useEffect(() => {
    get();
  }, []);

  const create = async () => {
    const searchText = inputRef.current.inputRef.current.value;
    const id = uuidv4();

    try {
      setCreating({ creating: true, createError: false })
      await search.search({ id, searchText })
      setCreating({ creating: false, createError: false })
      inputRef.current.inputRef.current.value = '';
      window.location.reload();
    } catch (error) {
      setCreating({ creating: false, createError: error.message })
    }
  }

  const get = async () => {
    setLoading({ loading: true, error: false });

    try {
      const searches = await search.getAll();

      console.log(searches)

      setSearches(searches);
      setLoading({ loading: false, error: false })
    } catch (error) {
      setLoading({ loading: false, error: error.message })
    }
  }

  const cancelAndDeleteAll = () => {
    // todo
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
            <Table selectable>
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
                {searches.map((search, index) => <Table.Row  style={{ cursor: 'pointer'}} key={index}>
                  <Table.Cell><SearchIcon state={search.state} alt={search.state}/></Table.Cell>
                  <Table.Cell>{search.startedAt}</Table.Cell>
                  <Table.Cell>{search.searchText}</Table.Cell>
                  <Table.Cell>{search.responseCount}</Table.Cell>
                  <Table.Cell>{search.fileCount} ({search.lockedFileCount})</Table.Cell>
                  <Table.Cell>{search.state.includes('Completed') ? 
                    <Icon name="trash alternate" color='red'/> : 
                    <Icon name="stop circle" color="red"/>}
                  </Table.Cell>
                </Table.Row>)}
              </Table.Body>
            </Table>
          </Card.Content>
      </Card>
    </div>
  )
};

export default Searches;