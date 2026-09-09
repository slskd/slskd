import './Transfers.css';
import * as transfersLibrary from '../../lib/transfers';
import { LoaderSegment, PlaceholderSegment } from '../Shared';
import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';
import React, { useEffect, useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import { Pagination } from 'semantic-ui-react';

const PER_PAGE = 100;

const flattenTransfers = (transfers) =>
  transfers.reduce(
    (users, user) =>
      users.concat(
        user.directories.reduce(
          (directories, directory) => directories.concat(directory.files),
          [],
        ),
      ),
    [],
  );

const getRetryableFiles = ({ files, retryOption }) => {
  switch (retryOption) {
    case 'Errored':
      return files.filter((file) =>
        [
          'Completed, TimedOut',
          'Completed, Errored',
          'Completed, Rejected',
        ].includes(file.state),
      );
    case 'Cancelled':
      return files.filter((file) => file.state === 'Completed, Cancelled');
    case 'All':
      return files.filter((file) =>
        transfersLibrary.isStateRetryable(file.state),
      );
    default:
      return [];
  }
};

const getCancellableFiles = ({ cancelOption, files }) => {
  switch (cancelOption) {
    case 'All':
      return files.filter((file) =>
        transfersLibrary.isStateCancellable(file.state),
      );
    case 'Queued':
      return files.filter((file) =>
        ['Queued, Locally', 'Queued, Remotely'].includes(file.state),
      );
    case 'In Progress':
      return files.filter((file) => file.state === 'InProgress');
    default:
      return [];
  }
};

const getRemovableFiles = ({ files, removeOption }) => {
  switch (removeOption) {
    case 'Succeeded':
      return files.filter((file) => file.state === 'Completed, Succeeded');
    case 'Errored':
      return files.filter((file) =>
        [
          'Completed, TimedOut',
          'Completed, Errored',
          'Completed, Rejected',
        ].includes(file.state),
      );
    case 'Cancelled':
      return files.filter((file) => file.state === 'Completed, Cancelled');
    case 'Completed':
      return files.filter((file) => file.state.includes('Completed'));
    default:
      return [];
  }
};

const Transfers = ({ direction, server }) => {
  const [connecting, setConnecting] = useState(true);
  const [page, setPage] = useState(1);
  const [transfers, setTransfers] = useState([]);
  const [totalCount, setTotalCount] = useState(0);

  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [removing, setRemoving] = useState(false);

  const fetch = async () => {
    try {
      const { totalCount: count, transfers: items } =
        await transfersLibrary.getAll({
          direction,
          limit: PER_PAGE,
          offset: (page - 1) * PER_PAGE,
        });
      const lastPage = Math.max(1, Math.ceil(count / PER_PAGE));

      setTotalCount(count);

      if (page > lastPage) {
        setPage(lastPage);
        return;
      }

      setTransfers(items);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    }
  };

  useEffect(() => {
    setConnecting(true);

    const init = async () => {
      await fetch();
      setConnecting(false);
    };

    init();
    const interval = window.setInterval(fetch, 1_000);

    return () => {
      clearInterval(interval);
    };
  }, [direction, page]); // eslint-disable-line react-hooks/exhaustive-deps

  useMemo(() => {
    // this is used to prevent weird update issues if switching
    // between uploads and downloads.  useEffect fires _after_ the
    // prop 'direction' updates, meaning there's a flash where the
    // screen contents switch to the new direction for a brief moment
    // before the connecting animation shows.  this memo fires the instant
    // the direction prop changes, preventing this flash.
    setConnecting(true);
    setPage(1);
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  const getAllFiles = async () => {
    const response = await transfersLibrary.getAll({ direction });
    return flattenTransfers(response.transfers).filter(
      (file) => file.direction.toLowerCase() === direction,
    );
  };

  const retry = async ({ file, suppressStateChange = false }) => {
    const { filename, size, username } = file;

    try {
      if (!suppressStateChange) setRetrying(true);
      await transfersLibrary.download({
        files: [{ filename, size }],
        username,
      });
      if (!suppressStateChange) setRetrying(false);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) setRetrying(false);
    }
  };

  const retryAll = async (transfersToRetry) => {
    setRetrying(true);
    await Promise.all(
      transfersToRetry.map((file) =>
        retry({ file, suppressStateChange: true }),
      ),
    );
    setRetrying(false);
    await fetch();
  };

  const retryAllMatching = async ({ retryOption }) => {
    setRetrying(true);

    try {
      const files = await getAllFiles();
      await retryAll(getRetryableFiles({ files, retryOption }));
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRetrying(false);
    }
  };

  const cancel = async ({ file, suppressStateChange = false }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) setCancelling(true);
      await transfersLibrary.cancel({ direction, id, username });
      if (!suppressStateChange) setCancelling(false);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) setCancelling(false);
    }
  };

  const cancelAll = async (transfersToCancel) => {
    setCancelling(true);
    await Promise.all(
      transfersToCancel.map((file) =>
        cancel({ file, suppressStateChange: true }),
      ),
    );
    setCancelling(false);
    await fetch();
  };

  const cancelAllMatching = async ({ cancelOption }) => {
    setCancelling(true);

    try {
      const files = await getAllFiles();
      await cancelAll(getCancellableFiles({ cancelOption, files }));
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setCancelling(false);
    }
  };

  const remove = async ({ file, suppressStateChange = false }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) setRemoving(true);
      await transfersLibrary.cancel({ direction, id, remove: true, username });
      if (!suppressStateChange) setRemoving(false);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) setRemoving(false);
    }
  };

  const removeAll = async (transfersToRemove) => {
    setRemoving(true);
    await Promise.all(
      transfersToRemove.map((file) =>
        remove({ file, suppressStateChange: true }),
      ),
    );
    setRemoving(false);
    await fetch();
  };

  const removeAllMatching = async ({ removeOption }) => {
    setRemoving(true);

    try {
      const files = await getAllFiles();
      await removeAll(getRemovableFiles({ files, removeOption }));
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRemoving(false);
    }
  };

  if (connecting) {
    return <LoaderSegment />;
  }

  return (
    <>
      <TransfersHeader
        cancelling={cancelling}
        direction={direction}
        onCancelAll={cancelAllMatching}
        onRemoveAll={removeAllMatching}
        onRetryAll={retryAllMatching}
        removing={removing}
        retrying={retrying}
        server={server}
        transfers={transfers}
      />
      {totalCount > PER_PAGE && (
        <div className="transfers-pagination">
          <Pagination
            activePage={page}
            onPageChange={(_event, data) => setPage(data.activePage)}
            totalPages={Math.ceil(totalCount / PER_PAGE)}
          />
        </div>
      )}
      {transfers.length === 0 ? (
        <PlaceholderSegment
          caption={`No ${direction}s to display`}
          icon={direction}
        />
      ) : (
        transfers.map((user) => (
          <TransferGroup
            cancel={cancel}
            cancelAll={cancelAll}
            direction={direction}
            key={user.username}
            remove={remove}
            removeAll={removeAll}
            retry={retry}
            retryAll={retryAll}
            user={user}
          />
        ))
      )}
    </>
  );
};

export default Transfers;
