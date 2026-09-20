/* eslint-disable n/global-require, react/prop-types */

import { createSearchHubConnection } from '../../lib/hubFactory';
import * as searchesLibrary from '../../lib/searches';
import Searches from './Searches';
import React from 'react';
import ReactDOM from 'react-dom';
import { act } from 'react-dom/test-utils';
import { MemoryRouter, Route } from 'react-router-dom';

jest.mock('../../lib/hubFactory', () => ({
  createSearchHubConnection: jest.fn(),
}));

jest.mock('../../lib/searches', () => ({
  create: jest.fn(),
  getAll: jest.fn(),
  getStatus: jest.fn(),
  remove: jest.fn(),
  stop: jest.fn(),
}));

jest.mock('react-toastify', () => ({
  toast: { error: jest.fn() },
}));

jest.mock('uuid', () => ({ v4: () => 'new-search-id' }));

jest.mock('semantic-ui-react', () => {
  const Wrapper = ({ children }) => <div>{children}</div>;
  const Input = require('react').forwardRef((_props, ref) => (
    <input ref={ref} />
  ));

  return {
    Button: Wrapper,
    Icon: Wrapper,
    Input,
    Pagination: ({ activePage, onPageChange }) => (
      <button
        id="next-page"
        onClick={() => onPageChange({}, { activePage: activePage + 1 })}
        type="button"
      >
        Next
      </button>
    ),
    Segment: Wrapper,
  };
});

jest.mock('../Shared/ErrorSegment', () => ({ caption }) => (
  <div>{`Error: ${caption}`}</div>
));
jest.mock('../Shared/LoaderSegment', () => () => <div>Loading</div>);
jest.mock('../Shared/PlaceholderSegment', () => ({ caption }) => (
  <div>{caption}</div>
));
jest.mock('./Detail/SearchDetail', () => ({ search }) => (
  <div id="search-detail">{`${search.id}:${search.searchText}`}</div>
));
jest.mock('./List/SearchList', () => ({ searches }) => (
  <div id="search-list">{Object.keys(searches).join(',')}</div>
));

const flush = async () => {
  await act(async () => {
    await new Promise((resolve) => {
      setTimeout(resolve, 0);
    });
  });
};

describe('Searches pagination', () => {
  let container;
  let handlers;
  let hub;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.append(container);
    handlers = {};
    hub = {
      on: jest.fn((name, handler) => {
        handlers[name] = handler;
      }),
      onclose: jest.fn(),
      onreconnected: jest.fn(),
      onreconnecting: jest.fn(),
      start: jest.fn().mockResolvedValue(undefined),
      stop: jest.fn().mockResolvedValue(undefined),
    };
    createSearchHubConnection.mockReturnValue(hub);
  });

  afterEach(() => {
    act(() => {
      ReactDOM.unmountComponentAtNode(container);
    });
    container.remove();
    jest.clearAllMocks();
  });

  it('loads pages and clamps the page after the total count shrinks', async () => {
    expect.hasAssertions();
    searchesLibrary.getAll.mockResolvedValue({
      searches: [{ id: 'page', searchText: 'Page' }],
      totalCount: 201,
    });

    act(() => {
      ReactDOM.render(
        <MemoryRouter initialEntries={['/searches']}>
          <Route path="/searches/:id?">
            <Searches server={{ isConnected: true }} />
          </Route>
        </MemoryRouter>,
        container,
      );
    });
    await flush();

    act(() => container.querySelector('#next-page').click());
    await flush();

    expect(searchesLibrary.getAll).toHaveBeenCalledWith({
      limit: 100,
      offset: 100,
    });
    expect(container.querySelector('#search-list').textContent).toContain(
      'page',
    );

    searchesLibrary.getAll.mockResolvedValue({
      searches: [{ id: 'remaining', searchText: 'Remaining' }],
      totalCount: 1,
    });
    await act(async () => handlers.delete({ id: 'unrelated' }));
    await flush();

    expect(searchesLibrary.getAll).toHaveBeenLastCalledWith({
      limit: 100,
      offset: 0,
    });
    expect(container.querySelector('#search-list').textContent).toBe(
      'remaining',
    );
  });

  it('loads a direct detail URL and applies live updates', async () => {
    expect.hasAssertions();
    searchesLibrary.getAll.mockResolvedValue({ searches: [], totalCount: 0 });
    searchesLibrary.getStatus.mockResolvedValue({
      id: 'old-search',
      searchText: 'Original',
    });

    act(() => {
      ReactDOM.render(
        <MemoryRouter initialEntries={['/searches/old-search']}>
          <Route path="/searches/:id?">
            <Searches server={{ isConnected: true }} />
          </Route>
        </MemoryRouter>,
        container,
      );
    });
    await flush();
    await flush();

    expect(searchesLibrary.getStatus).toHaveBeenCalledWith({
      id: 'old-search',
    });
    expect(container.querySelector('#search-detail').textContent).toContain(
      'Original',
    );

    act(() => handlers.update({ id: 'old-search', searchText: 'Updated' }));

    expect(container.querySelector('#search-detail').textContent).toContain(
      'Updated',
    );
  });
});
