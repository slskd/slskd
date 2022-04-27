import React, { useState } from 'react';

import SearchIcon from '../SearchIcon';

import {
  Table,
  Icon
} from 'semantic-ui-react';

const SearchListRow = ({ search, index, onRemove, onStop }) => {
  const [working, setWorking] = useState(false);

  const invoke = async (func) => {
    setWorking(true);
    await func();
    setWorking(false);
  }

  const ActionIcon = () => {
    if (working) {
      return (<Icon name='hourglass half'/>)
    }

    if (search.state.includes('Completed')) {
      return (<Icon
        name="trash alternate"
        color='red' 
        onClick={() => invoke(() => onRemove(search))}
      />)
    }

    return (<Icon
      name="stop circle"
      color="red"
      onClick={() => invoke(() => onStop(search))}
    />)
  }

  return (
    <Table.Row
      disabled={working}
      style={{ cursor: working ? 'wait' : 'pointer' }}
    >
      <Table.Cell>{working}<SearchIcon state={search.state}/></Table.Cell>
      <Table.Cell>{search.startedAt}</Table.Cell>
      <Table.Cell>{search.searchText}</Table.Cell>
      <Table.Cell>{search.responseCount}</Table.Cell>
      <Table.Cell>{search.fileCount} ({search.lockedFileCount})</Table.Cell>
      <Table.Cell><ActionIcon/></Table.Cell>
    </Table.Row>
  )
}

export default SearchListRow;