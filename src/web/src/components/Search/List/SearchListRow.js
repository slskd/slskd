import React from 'react';

import SearchIcon from '../SearchIcon';

import {
  Table,
  Icon,
  Checkbox
} from 'semantic-ui-react';

const SearchListRow = ({ search, index, onRemove, onStop }) => {
  return (
    <Table.Row>
      <Table.Cell><Checkbox/></Table.Cell>
      <Table.Cell><SearchIcon state={search.state}/></Table.Cell>
      <Table.Cell>{search.searchText}</Table.Cell>
      <Table.Cell>{search.fileCount}</Table.Cell>
      <Table.Cell><Icon name="lock" color="yellow" size="small"/>{search.lockedFileCount}</Table.Cell>
      <Table.Cell>{search.responseCount}</Table.Cell>
      <Table.Cell>{search.endedAt ? new Date(search.endedAt).toLocaleTimeString() : '-'}</Table.Cell>
    </Table.Row>
  )
}

export default SearchListRow;