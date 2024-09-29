import { list } from '../../../lib/events';
import { LoaderSegment } from '../../Shared';
import React, { useEffect, useState } from 'react';
import { Icon, Pagination, Popup, Table } from 'semantic-ui-react';

const PER_PAGE = 50;

const Events = () => {
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [events, setEvents] = useState([]);

  const fetch = async () => {
    setLoading(true);

    const eventResult = await list({
      limit: PER_PAGE,
      offset: (page - 1) * PER_PAGE,
    });

    setEvents(eventResult);
    setLoading(false);

    return eventResult;
  };

  const paginationChanged = ({ activePage }) => {
    console.log(activePage);
    setPage(activePage);
  };

  useEffect(() => {
    fetch();
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) {
    return <LoaderSegment />;
  }

  return (
    <>
      <Table
        className="events-table"
        compact="very"
      >
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell classname="">Id</Table.HeaderCell>
            <Table.HeaderCell classname="">Timestamp</Table.HeaderCell>
            <Table.HeaderCell classname="">Type</Table.HeaderCell>
            <Table.HeaderCell classname="">Data</Table.HeaderCell>
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
                <Table.Cell>{event.timestamp}</Table.Cell>
                <Table.Cell>{event.type}</Table.Cell>
                <Table.Cell className="events-table-data">
                  {event.data}
                </Table.Cell>
              </Table.Row>
            ))
          )}
        </Table.Body>
      </Table>
      <Pagination
        activePage={page}
        onPageChange={(event, data) => paginationChanged({ ...data })}
        totalPages={100}
      />
    </>
  );
};

export default Events;
