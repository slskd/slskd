import './Transfers.css';

import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { toast } from 'react-toastify';

import * as autoReplaceLibrary from '../../lib/autoReplace';
import * as transfersLibrary from '../../lib/transfers';
import { LoaderSegment, PlaceholderSegment } from '../Shared';
import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';

const AUTO_REPLACE_INTERVAL_MS = 30000; // Check every 30 seconds
const AUTO_REPLACE_THRESHOLD = 5; // 5% size difference threshold

const Transfers = ({ direction, server }) => {
  const [connecting, setConnecting] = useState(true);
  const [transfers, setTransfers] = useState([]);

  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [removing, setRemoving] = useState(false);

  const [autoReplaceEnabled, setAutoReplaceEnabled] = useState(false);
  const autoReplaceThreshold = AUTO_REPLACE_THRESHOLD;
  const autoReplaceIntervalRef = useRef(null);

  const fetch = async () => {
    try {
      const response = await transfersLibrary.getAll({ direction });
      setTransfers(response);
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
    const interval = window.setInterval(fetch, 1000);

    return () => {
      clearInterval(interval);
    };
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  useMemo(() => {
    // this is used to prevent weird update issues if switching
    // between uploads and downloads.  useEffect fires _after_ the
    // prop 'direction' updates, meaning there's a flash where the
    // screen contents switch to the new direction for a brief moment
    // before the connecting animation shows.  this memo fires the instant
    // the direction prop changes, preventing this flash.
    setConnecting(true);
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  const retry = async ({ file, suppressStateChange = false }) => {
    const { filename, size, username } = file;

    try {
      if (!suppressStateChange) {
        setRetrying(true);
      }

      await transfersLibrary.download({
        files: [{ filename, size }],
        username,
      });
      if (!suppressStateChange) {
        setRetrying(false);
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) {
        setRetrying(false);
      }
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
  };

  const cancel = async ({ file, suppressStateChange = false }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) {
        setCancelling(true);
      }

      await transfersLibrary.cancel({ direction, id, username });
      if (!suppressStateChange) {
        setCancelling(false);
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) {
        setCancelling(false);
      }
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
  };

  const remove = async ({ file, suppressStateChange = false }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) {
        setRemoving(true);
      }

      await transfersLibrary.cancel({ direction, id, remove: true, username });
      if (!suppressStateChange) {
        setRemoving(false);
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) {
        setRemoving(false);
      }
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
  };

  // Auto-replace logic for stuck downloads
  const processAutoReplace = useCallback(async () => {
    if (!autoReplaceEnabled || direction !== 'download') {
      return;
    }

    try {
      const result = await autoReplaceLibrary.processStuckDownloads({
        threshold: autoReplaceThreshold,
      });

      if (result?.replaced > 0) {
        toast.success(`Auto-replaced ${result.replaced} stuck download(s)`);
      }
    } catch (error) {
      console.error('Auto-replace error:', error);
      // Don't toast on every interval failure, just log it
    }
  }, [autoReplaceEnabled, autoReplaceThreshold, direction]);

  // Set up auto-replace interval
  useEffect(() => {
    if (autoReplaceEnabled && direction === 'download') {
      // Run immediately on enable
      processAutoReplace();

      // Then run on interval
      autoReplaceIntervalRef.current = window.setInterval(
        processAutoReplace,
        AUTO_REPLACE_INTERVAL_MS,
      );

      toast.info('Auto-replace enabled. Checking for stuck downloads...');
    } else if (autoReplaceIntervalRef.current) {
      clearInterval(autoReplaceIntervalRef.current);
      autoReplaceIntervalRef.current = null;
    }

    return () => {
      if (autoReplaceIntervalRef.current) {
        clearInterval(autoReplaceIntervalRef.current);
      }
    };
  }, [autoReplaceEnabled, direction, processAutoReplace]);

  const handleAutoReplaceChange = (enabled) => {
    setAutoReplaceEnabled(enabled);
    if (!enabled) {
      toast.info('Auto-replace disabled');
    }
  };

  if (connecting) {
    return <LoaderSegment />;
  }

  return (
    <>
      <TransfersHeader
        autoReplaceEnabled={autoReplaceEnabled}
        autoReplaceThreshold={autoReplaceThreshold}
        cancelling={cancelling}
        direction={direction}
        onAutoReplaceChange={handleAutoReplaceChange}
        onCancelAll={cancelAll}
        onRemoveAll={removeAll}
        onRetryAll={retryAll}
        removing={removing}
        retrying={retrying}
        server={server}
        transfers={transfers}
      />
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
