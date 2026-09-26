import ClearAllButton from '../../Shared/ClearAllButton';
import ErrorSegment from '../../Shared/ErrorSegment';
import Switch from '../../Shared/Switch';
import SearchListRow from './SearchListRow';
import React from 'react';
import { Card, Icon, Loader, Table } from 'semantic-ui-react';

const SearchList = ({
  connecting = false,
  error = undefined,
  onClearAll = async () => {},
  onRemove = () => {},
  onStop = () => {},
  searches = {},
}) => {
  return (
    <Card
      className="search-list-card"
      raised
    >
      <Card.Content>
        <div className="search-list-header">
          <span>
            {Object.keys(searches).length} search
            {Object.keys(searches).length === 1 ? '' : 'es'}
          </span>
          <ClearAllButton
            confirm="Delete every finished search? Searches that are still running are kept."
            disabled={Object.keys(searches).length === 0}
            onConfirm={onClearAll}
          />
        </div>
        <div className="search-list-wrapper">
          <Switch
            connecting={
              connecting && (
                <Loader
                  active
                  inline="centered"
                  size="small"
                />
              )
            }
            error={error && <ErrorSegment caption={error} />}
          >
            <Table
              className="unstackable"
              size="large"
            >
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell className="search-list-action">
                    <Icon name="info circle" />
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-phrase">
                    Search
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-files">
                    Files
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-locked">
                    Locked
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-responses">
                    Responses
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-started">
                    Ended
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-action" />
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {Object.values(searches)
                  .sort((a, b) => new Date(b.startedAt) - new Date(a.startedAt))
                  .map((search) => (
                    <SearchListRow
                      key={search.id}
                      onRemove={onRemove}
                      onStop={onStop}
                      search={search}
                    />
                  ))}
              </Table.Body>
            </Table>
          </Switch>
        </div>
      </Card.Content>
    </Card>
  );
};

export default SearchList;
