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

  const ActionIcon = ({ ...props }) => {
    if (working) {
      return (<Icon name='spinner' loading {...props}/>)
    }

    if (search.state.includes('Completed')) {
      return (<Icon
        name="trash alternate"
        color='red' 
        onClick={() => invoke(() => onRemove(search))}
        {...props}
      />)
    }

    return (<Icon
      name="stop"
      color="red"
      onClick={() => invoke(() => onStop(search))}
      {...props}
    />)
  }
  // 

  return (
    <Table.Row
      disabled={working}
      style={{ cursor: working ? 'wait' : 'pointer' }}
    >
      <Table.Cell><SearchIcon state={search.state} disabled={working}/></Table.Cell>
      <Table.Cell>{search.searchText}</Table.Cell>
      <Table.Cell>{search.fileCount}</Table.Cell>
      <Table.Cell><Icon name="lock" color="yellow" size="small"/>{search.lockedFileCount}</Table.Cell>
      <Table.Cell>{search.responseCount}</Table.Cell>
      <Table.Cell>{new Date(search.startedAt).toLocaleTimeString()}</Table.Cell>
      <Table.Cell><ActionIcon disabled={working}  className="search-list-action-icon"/></Table.Cell>
    </Table.Row>
  )
}

export default SearchListRow;