import React, { useState } from 'react';

import {
  Link,
  useRouteMatch,
} from 'react-router-dom';

import {
  Table,
  Icon,
} from 'semantic-ui-react';

import SearchStatusIcon from '../SearchStatusIcon';
import SearchActionIcon from './SearchActionIcon';

const SearchListRow = ({ search, onRemove, onStop }) => {
  const [working, setWorking] = useState(false);
  const match = useRouteMatch();

  const invoke = async (func) => {
    setWorking(true);

    try {
      await func();
    } catch (error) {
      console.error(error);
    } finally {
      setWorking(false);
    }
  }

  return (
    <Table.Row
      disabled={working}
      style={{ cursor: working ? 'wait' : undefined }}
    >
      <Table.Cell>
        <SearchStatusIcon
          state={search.state}
        />
      </Table.Cell>
      <Table.Cell><Link to={`${match.url}/${search.id}`}>{search.searchText}</Link></Table.Cell>
      <Table.Cell>{search.fileCount}</Table.Cell>
      <Table.Cell><Icon name="lock" color="yellow" size="small"/>{search.lockedFileCount}</Table.Cell>
      <Table.Cell>{search.responseCount}</Table.Cell>
      <Table.Cell>{search.endedAt ? new Date(search.endedAt).toLocaleTimeString() : '-'}</Table.Cell>
      <Table.Cell>
        <SearchActionIcon
          search={search}
          loading={working}
          onRemove={() => invoke(() => onRemove(search))}
          onStop={() => invoke(() => onStop(search))}
          style={{ cursor: 'pointer' }}
        />
      </Table.Cell>
    </Table.Row>
  )
}

export default SearchListRow;