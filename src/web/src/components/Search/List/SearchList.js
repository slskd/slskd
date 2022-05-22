import React, { useMemo }  from 'react';

import SearchListRow from './SearchListRow';
import ErrorSegment from '../../Shared/ErrorSegment';
import Switch from '../../Shared/Switch';

import {
  Card,
  Table,
  Icon,
  Loader,
  Segment,
  Header,
  List,
} from 'semantic-ui-react';

const SearchList = ({
  connecting = false,
  error = undefined,
  searches = {},
  onRemove = () => { },
  onStop = () => { },
}) => {
  const searchCount = useMemo(() => {
    return Object.values(searches).length
  }, [searches])

  return (
    <Card className='search-list-card' raised>
      <Card.Content>
        <div style={{marginTop: -20 }}>
          <Switch
            connecting={connecting && <Loader active inline='centered' size='small'/>}
            error={error && <ErrorSegment caption={error} />}
          >
            <Header size='small' className='filelist-header'>
              <div>
                <Icon size='large' name='search'/>
                Searches
              </div>
            </Header>
            <List>
              <List.Item>
                <Switch
                  noSearches={!searchCount && 
                    <Segment basic style={{opacity: .5}} textAlign='center'>No searches to display</Segment>
                  }
                >
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
                </Switch>
              </List.Item>
            </List>
          </Switch>
        </div>
      </Card.Content>
    </Card>
  )
};

export default SearchList;