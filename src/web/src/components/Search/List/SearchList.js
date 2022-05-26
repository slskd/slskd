import React from 'react';

import SearchListRow from './SearchListRow';
import ErrorSegment from '../../Shared/ErrorSegment';
import Switch from '../../Shared/Switch';

import {
  Card,
  Table,
  Icon,
  Loader,
  List,
} from 'semantic-ui-react';

const SearchList = ({
  connecting = false,
  error = undefined,
  searches = {},
  onRemove = () => { },
  onStop = () => { },
}) => {
  return (
    <Card className='search-list-card' raised>
      <Card.Content>
        <div>
          <Switch
            connecting={connecting && <Loader active inline='centered' size='small'/>}
            error={error && <ErrorSegment caption={error} />}
          >
            <List>
              <List.Item>
                <Table size='large'>
                  <Table.Header>
                    <Table.Row>
                      <Table.HeaderCell className="search-list-action"><Icon name="info circle"/></Table.HeaderCell>
                      <Table.HeaderCell className="search-list-phrase">Search</Table.HeaderCell>
                      <Table.HeaderCell className="search-list-files">Files</Table.HeaderCell>
                      <Table.HeaderCell className="search-list-locked">Locked</Table.HeaderCell>
                      <Table.HeaderCell className="search-list-responses">Responses</Table.HeaderCell>
                      <Table.HeaderCell className="search-list-started">Ended</Table.HeaderCell>
                      <Table.HeaderCell className="search-list-action"></Table.HeaderCell>
                    </Table.Row>
                  </Table.Header>
                  <Table.Body>
                    {Object.values(searches)
                      .sort((a, b) => (new Date(b.startedAt) - new Date(a.startedAt)))
                      .map((search, index) => <SearchListRow
                        search={search}
                        key={index}
                        onRemove={onRemove}
                        onStop={onStop}
                      />)}
                  </Table.Body>
                </Table>
              </List.Item>
            </List>
          </Switch>
        </div>
      </Card.Content>
    </Card>
  );
};

export default SearchList;