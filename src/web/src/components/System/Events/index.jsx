import { list } from '../../../lib/events';
import { LoaderSegment } from '../../Shared';
import React, { useEffect, useState } from 'react';
import { Icon, Pagination, Popup, Table } from 'semantic-ui-react';

const PER_PAGE = 10;

const replaceHyphensWithNonBreakingEquivalent = (string) =>
  string?.replaceAll('-', '‑');

const Events = () => {
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(false);
  const [events, setEvents] = useState([]);

  const fetch = async () => {
    setLoading(true);

    const { events: items, totalCount } = await list({
      limit: PER_PAGE,
      offset: (page - 1) * PER_PAGE,
    });

    const tp = Math.ceil(totalCount / PER_PAGE);

    setEvents(items);
    setTotalPages(Number.isNaN(tp) ? 0 : tp);
    setLoading(false);

    return items;
  };

  const paginationChanged = ({ activePage }) => {
    if (activePage >= 1) {
      setPage(activePage);
    }
  };

  useEffect(() => {
    fetch();
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) {
    return <LoaderSegment />;
  }

  return (
    <>
      <div className="header-buttons">
        <Pagination
          activePage={page}
          className="header-buttons"
          onPageChange={(event, data) => paginationChanged({ ...data })}
          totalPages={totalPages}
        />
      </div>
      <Table
        className="events-table, unstackable"
        compact="very"
      >
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell className="events-list-id">Id</Table.HeaderCell>
            <Table.HeaderCell className="events-list-timestamp">
              Timestamp
            </Table.HeaderCell>
            <Table.HeaderCell className="events-list-type">
              Type
            </Table.HeaderCell>
            <Table.HeaderCell className="events-list-data">
              Data
            </Table.HeaderCell>
          </Table.Row>
        </Table.Header>
        <Table.Body className="events-table-body">
          {events?.length === 0 ? (
            <Table.Row>
              <Table.Cell
                colSpan={99}
                style={{
                  opacity: 0.5,
                  padding: '10px !important',
                  textAlign: 'center',
                }}
              >
                No events
              </Table.Cell>
            </Table.Row>
          ) : (
            events.map((event) => (
              <Table.Row key={event.id}>
                <Table.Cell>
                  <Popup
                    content={event.id}
                    on="hover"
                    style={{ fontFamily: 'monospace', width: '400px' }}
                    trigger={<Icon name="info circle" />}
                    wide="very"
                  />
                </Table.Cell>
                <Table.Cell>
                  {replaceHyphensWithNonBreakingEquivalent(event.timestamp)}
                </Table.Cell>
                <Table.Cell>{event.type}</Table.Cell>
                <Table.Cell className="events-table-data">
                  {JSON.stringify(JSON.parse(event.data), null, 2)}
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>
    </>
  );
};

export default Events;
