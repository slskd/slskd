import './Search.css';
import { createSearchHubConnection } from '../../lib/hubFactory';
import * as library from '../../lib/searches';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import SearchDetail from './Detail/SearchDetail';
import SearchList from './List/SearchList';
import React, { useEffect, useRef, useState } from 'react';
import { useHistory, useParams, useRouteMatch } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Icon, Input, Pagination, Segment } from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const PER_PAGE = 100;

const Searches = ({ server } = {}) => {
  const [connecting, setConnecting] = useState(true);
  const [error, setError] = useState(undefined);
  const [loading, setLoading] = useState(false);
  const [loadingSearch, setLoadingSearch] = useState(false);
  const [page, setPage] = useState(1);
  const [searches, setSearches] = useState({});
  const [selectedSearch, setSelectedSearch] = useState(undefined);
  const [totalCount, setTotalCount] = useState(0);

  const [removing, setRemoving] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [creating, setCreating] = useState(false);

  const inputRef = useRef();
  const pageRef = useRef(page);

  const { id: searchId } = useParams();
  const history = useHistory();
  const match = useRouteMatch();
  const listUrl = match.url.replace(`/${searchId}`, '');
  const totalPages = Math.ceil(totalCount / PER_PAGE);

  const fetchPage = async (requestedPage = pageRef.current) => {
    try {
      setLoading(true);

      const { searches: items, totalCount: count } = await library.getAll({
        limit: PER_PAGE,
        offset: (requestedPage - 1) * PER_PAGE,
      });
      const lastPage = Math.max(1, Math.ceil(count / PER_PAGE));

      setTotalCount(count);

      if (requestedPage > lastPage) {
        setPage(lastPage);
        return;
      }

      setSearches(
        items.reduce((accumulator, search) => {
          accumulator[search.id] = search;
          return accumulator;
        }, {}),
      );
      setError(undefined);
    } catch (fetchError) {
      console.error(fetchError);
      setError(fetchError);
    } finally {
      setLoading(false);
    }
  };

  const onConnecting = () => {
    setConnecting(true);
  };

  const onConnected = () => {
    setConnecting(false);
  };

  const onConnectionError = (connectionError) => {
    setConnecting(false);
    setError(connectionError);
  };

  useEffect(() => {
    onConnecting();

    const searchHub = createSearchHubConnection({
      includeInitialList: false,
    });

    searchHub.on('list', () => {
      fetchPage();
    });

    searchHub.on('update', (search) => {
      setSearches((old) =>
        old[search.id] ? { ...old, [search.id]: search } : old,
      );
      setSelectedSearch((old) => (old?.id === search.id ? search : old));
    });

    searchHub.on('delete', (search) => {
      setSelectedSearch((old) => {
        if (old?.id === search.id) {
          history.replace(listUrl);
          return undefined;
        }

        return old;
      });

      fetchPage();
    });

    searchHub.on('create', () => {
      fetchPage();
    });

    searchHub.onreconnecting((connectionError) =>
      onConnectionError(connectionError?.message ?? 'Disconnected'),
    );
    searchHub.onreconnected(async () => {
      await fetchPage();
      onConnected();
    });
    searchHub.onclose((connectionError) =>
      onConnectionError(connectionError?.message ?? 'Disconnected'),
    );

    const connect = async () => {
      try {
        onConnecting();
        await searchHub.start();
        await fetchPage();
        onConnected();
      } catch (connectionError) {
        toast.error(connectionError?.message ?? 'Failed to connect');
        onConnectionError(connectionError?.message ?? 'Failed to connect');
      }
    };

    connect();

    return () => {
      searchHub.stop();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    pageRef.current = page;

    if (!connecting) {
      fetchPage(page);
    }
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    let cancelled = false;

    const fetchSearch = async () => {
      if (!searchId || searches[searchId]) {
        return;
      }

      try {
        setLoadingSearch(true);
        const search = await library.getStatus({ id: searchId });

        if (!cancelled) {
          setSelectedSearch(search);
        }
      } catch (fetchError) {
        if (!cancelled) {
          toast.error(
            fetchError?.response?.data ??
              fetchError?.message ??
              'Search not found',
          );
          history.replace(listUrl);
        }
      } finally {
        if (!cancelled) {
          setLoadingSearch(false);
        }
      }
    };

    if (!searchId) {
      setSelectedSearch(undefined);
    } else if (searches[searchId]) {
      setSelectedSearch(searches[searchId]);
    } else if (selectedSearch?.id !== searchId) {
      fetchSearch();
    }

    return () => {
      cancelled = true;
    };
  }, [searchId]); // eslint-disable-line react-hooks/exhaustive-deps

  // create a new search, and optionally navigate to it to display the details
  // we do this if the user clicks the search icon, or repeats an existing search
  const create = async ({ navigate = false, search } = {}) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = search || ref.value;
    const id = uuidv4();

    try {
      setCreating(true);
      await library.create({ id, searchText });

      try {
        ref.value = '';
        ref.focus();
      } catch {
        // we are probably repeating an existing search; the input isn't mounted.  no-op.
      }

      pageRef.current = 1;
      setPage(1);
      await fetchPage(1);
      setCreating(false);

      if (navigate) {
        history.push(`${listUrl}/${id}`);
      }
    } catch (createError) {
      console.error(createError);
      toast.error(
        createError?.response?.data ?? createError?.message ?? createError,
      );
      setCreating(false);
    }
  };

  // delete a search
  const remove = async (search) => {
    try {
      setRemoving(true);

      await library.remove({ id: search.id });

      if (search.id === searchId) {
        setSelectedSearch(undefined);
        history.replace(listUrl);
      }

      await fetchPage();

      setRemoving(false);
    } catch (removeError) {
      console.error(removeError);
      toast.error(
        removeError?.response?.data ?? removeError?.message ?? removeError,
      );
      setRemoving(false);
    }
  };

  // stop an in-progress search
  const stop = async (search) => {
    try {
      setStopping(true);
      await library.stop({ id: search.id });
      setStopping(false);
    } catch (stoppingError) {
      console.error(stoppingError);
      toast.error(
        stoppingError?.response?.data ??
          stoppingError?.message ??
          stoppingError,
      );
      setStopping(false);
    }
  };

  if (connecting || (searchId && loadingSearch)) {
    return <LoaderSegment />;
  }

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  // if searchId is not null, there's an id in the route.
  // display the details for the search, if there is one
  if (searchId) {
    const search = searches[searchId] ?? selectedSearch;

    if (search?.id === searchId) {
      return (
        <SearchDetail
          creating={creating}
          disabled={!server?.isConnected}
          onCreate={create}
          onRemove={remove}
          onStop={stop}
          removing={removing}
          search={search}
          stopping={stopping}
        />
      );
    }

    return <LoaderSegment />;
  }

  inputRef?.current?.inputRef?.current.focus();

  return (
    <>
      <Segment
        className="search-segment"
        raised
      >
        <div className="search-segment-icon">
          <Icon
            name="search"
            size="big"
          />
        </div>
        <Input
          action={
            <>
              <Button
                disabled={creating || !server.isConnected}
                icon="plus"
                onClick={create}
              />
              <Button
                disabled={creating || !server.isConnected}
                icon="search"
                onClick={() => create({ navigate: true })}
              />
            </>
          }
          className="search-input"
          disabled={creating || !server.isConnected}
          input={
            <input
              data-lpignore="true"
              placeholder={
                server.isConnected
                  ? 'Search phrase'
                  : 'Connect to server to perform a search'
              }
              type="search"
            />
          }
          loading={creating}
          onKeyUp={(keyUpEvent) => (keyUpEvent.key === 'Enter' ? create() : '')}
          placeholder="Search phrase"
          ref={inputRef}
          size="big"
        />
      </Segment>
      {totalPages > 1 && (
        <div className="search-pagination">
          <Pagination
            activePage={page}
            onPageChange={(_event, data) => setPage(data.activePage)}
            totalPages={totalPages}
          />
        </div>
      )}
      {loading ? (
        <LoaderSegment />
      ) : Object.keys(searches).length === 0 ? (
        <PlaceholderSegment
          caption="No searches to display"
          icon="search"
        />
      ) : (
        <SearchList
          connecting={connecting}
          error={error}
          onRemove={remove}
          onStop={stop}
          searches={searches}
        />
      )}
    </>
  );
};

export default Searches;
