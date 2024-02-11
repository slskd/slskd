import SearchStatusIcon from '../SearchStatusIcon';
import SearchActionIcon from './SearchActionIcon';
import React, { useState } from 'react';
import { Link, useRouteMatch } from 'react-router-dom';
import { Icon, Table } from 'semantic-ui-react';

const SearchListRow = ({ onRemove, onStop, search }) => {
  const [working, setWorking] = useState(false);
  const match = useRouteMatch();

  const invoke = async (function_) => {
    setWorking(true);

    try {
      await function_();
    } catch (error) {
      console.error(error);
    } finally {
      setWorking(false);
    }
  };

  return (
    <Table.Row
      disabled={working}
      style={{ cursor: working ? 'wait' : undefined }}
    >
      <Table.Cell>
        <SearchStatusIcon state={search.state} />
      </Table.Cell>
      <Table.Cell>
        <Link to={`${match.url}/${search.id}`}>{search.searchText}</Link>
      </Table.Cell>
      <Table.Cell>{search.fileCount}</Table.Cell>
      <Table.Cell>
        <Icon
          color="yellow"
          name="lock"
          size="small"
        />
        {search.lockedFileCount}
      </Table.Cell>
      <Table.Cell>{search.responseCount}</Table.Cell>
      <Table.Cell>
        {search.endedAt ? new Date(search.endedAt).toLocaleTimeString() : '-'}
      </Table.Cell>
      <Table.Cell>
        <SearchActionIcon
          loading={working}
          onRemove={() => invoke(() => onRemove(search))}
          onStop={() => invoke(() => onStop(search))}
          search={search}
          style={{ cursor: 'pointer' }}
        />
      </Table.Cell>
    </Table.Row>
  );
};

export default SearchListRow;
