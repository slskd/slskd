/* eslint-disable react/prop-types */

import * as transfersLibrary from '../../lib/transfers';
import Transfers from './Transfers';
import React from 'react';
import ReactDOM from 'react-dom';
import { act } from 'react-dom/test-utils';

jest.mock('../../lib/transfers', () => ({
  cancel: jest.fn().mockResolvedValue(undefined),
  download: jest.fn().mockResolvedValue(undefined),
  getAll: jest.fn(),
  isStateCancellable: (state) => state === 'Queued, Locally',
  isStateRemovable: (state) => state.includes('Completed'),
  isStateRetryable: (state) => state.includes('Completed'),
}));

jest.mock('react-toastify', () => ({
  toast: { error: jest.fn() },
}));

jest.mock('semantic-ui-react', () => ({
  Pagination: () => <div>Pagination</div>,
}));

jest.mock('../Shared', () => ({
  LoaderSegment: () => <div>Loading</div>,
  PlaceholderSegment: ({ caption }) => <div>{caption}</div>,
}));

jest.mock('./TransferGroup', () => ({ user }) => <div>{user.username}</div>);

jest.mock('./TransfersHeader', () => ({ onRemoveAll }) => (
  <button
    id="remove-all"
    onClick={() => onRemoveAll({ removeOption: 'Completed' })}
    type="button"
  >
    Remove all
  </button>
));

const group = (username, id, state) => ({
  directories: [
    {
      directory: 'music',
      files: [
        {
          direction: 'Download',
          filename: `${username}.mp3`,
          id,
          state,
          username,
        },
      ],
    },
  ],
  username,
});

const flush = async () => {
  await act(async () => {
    await new Promise((resolve) => {
      setTimeout(resolve, 0);
    });
  });
};

describe('Transfers pagination', () => {
  let container;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.append(container);
    transfersLibrary.getAll.mockImplementation(({ limit }) =>
      Promise.resolve(
        limit
          ? {
              totalCount: 101,
              transfers: [
                group('visible', 'visible-id', 'Completed, Succeeded'),
              ],
            }
          : {
              totalCount: 2,
              transfers: [
                group('visible', 'visible-id', 'Completed, Succeeded'),
                group('hidden', 'hidden-id', 'Completed, Cancelled'),
              ],
            },
      ),
    );
  });

  afterEach(() => {
    act(() => {
      ReactDOM.unmountComponentAtNode(container);
    });
    container.remove();
    jest.clearAllMocks();
  });

  it('uses an unpaged request for full-scope bulk actions', async () => {
    expect.hasAssertions();
    act(() => {
      ReactDOM.render(
        <Transfers
          direction="download"
          server={{ isConnected: true }}
        />,
        container,
      );
    });
    await flush();

    act(() => container.querySelector('#remove-all').click());
    await flush();
    await flush();

    expect(transfersLibrary.getAll).toHaveBeenCalledWith({
      direction: 'download',
    });
    expect(transfersLibrary.cancel).toHaveBeenCalledTimes(2);
    expect(transfersLibrary.cancel).toHaveBeenCalledWith({
      direction: 'download',
      id: 'hidden-id',
      remove: true,
      username: 'hidden',
    });
  });
});
