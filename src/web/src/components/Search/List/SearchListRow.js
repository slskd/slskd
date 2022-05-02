import React, { useState } from 'react';

import SearchIcon from '../SearchIcon';

import {
  Table,
  Icon,
  Checkbox
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
      return (<Icon name='spinner' loading/>)
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
      style={{ cursor: working ? 'wait' : undefined }}
    >
      <Table.Cell><SearchIcon state={search.state}/></Table.Cell>
      <Table.Cell><a href={`searches/${search.id}`}>{search.searchText}</a></Table.Cell>
      <Table.Cell>{search.fileCount}</Table.Cell>
      <Table.Cell><Icon name="lock" color="yellow" size="small"/>{search.lockedFileCount}</Table.Cell>
      <Table.Cell>{search.responseCount}</Table.Cell>
      <Table.Cell>{search.endedAt ? new Date(search.endedAt).toLocaleTimeString() : '-'}</Table.Cell>
      <Table.Cell style={{ cursor: 'pointer' }}>
        <ActionIcon/>
      </Table.Cell>
    </Table.Row>
  )
}

export default SearchListRow;