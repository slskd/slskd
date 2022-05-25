import React, { useState, useEffect, useMemo } from 'react';
import * as transfersLib from '../../lib/transfers';
import { toast } from 'react-toastify';

import { 
  LoaderSegment,
  PlaceholderSegment,
} from '../Shared';

import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';

import './Transfers.css';

const Transfers = ({ direction, server }) => {
  const [connecting, setConnecting] = useState(true);
  const [transfers, setTransfers] = useState([]);

  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [removing, setRemoving] = useState(false);

  useEffect(() => {
    setConnecting(true);

    const init = async () => {
      await fetch();
      setConnecting(false);
    }

    init();
    const interval = window.setInterval(fetch, 1000);
    
    return () => {
      clearInterval(interval);
    }
  }, [direction]);

  useMemo(() => {
    // this is used to prevent weird update issues if switching
    // between uploads and downloads.  useEffect fires _after_ the
    // prop 'direction' updates, meaning there's a flash where the 
    // screen contents switch to the new direction for a brief moment
    // before the connecting animation shows.  this memo fires the instant
    // the direction prop changes, preventing this flash.
    setConnecting(true);
  }, [direction]);

  const fetch = async () => {
    try {
      const response = await transfersLib.getAll({ direction });
      setTransfers(response);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    }
  }

  const retry = async ({ file, suppressStateChange = false }) => {
    const { username, filename, size } = file;
        
    try {
      if (!suppressStateChange) setRetrying(true);
      await transfersLib.download({username, files: [{filename, size }] });
      if (!suppressStateChange) setRetrying(false);
    } catch (error) {
      console.log(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) setRetrying(false);
    }
  }

  const retryAll = async (transfers) => {
    setRetrying(true);
    await Promise.all(transfers.map(file => retry({ file, suppressStateChange: true })))
    setRetrying(false);
  }

  const cancel = async ({ file, suppressStateChange = false }) => {
    const { username, id } = file;
    
    try {
      if (!suppressStateChange) setCancelling(true);
      await transfersLib.cancel({ direction, username, id });
      if (!suppressStateChange) setCancelling(false);
    } catch (error) {
      console.log(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) setCancelling(false);
    }
  }

  const cancelAll = async (transfers) => {
    setCancelling(true);
    await Promise.all(transfers.map(file => cancel({ file, suppressStateChange: true })));
    setCancelling(false);
  }

  const remove = async ({ file, suppressStateChange = false }) => {
    const { username, id } = file;

    try {
      if (!suppressStateChange) setRemoving(true);
      await transfersLib.cancel({ direction, username, id, remove: true });
      if (!suppressStateChange) setRemoving(false);
    } catch (error) {
      console.log(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) setRemoving(false);
    }
  }

  const removeAll = async (transfers) => {
    setRemoving(true);
    await Promise.all(transfers.map(file => remove({ file, suppressStateChange: true })));
    setRemoving(false);
  }

  if (connecting) {
    return <LoaderSegment/>
  }

  return (
    <>
      <TransfersHeader 
        direction={direction} 
        transfers={transfers} 
        server={server}
        onRetryAll={retryAll}
        retrying={retrying}
        onCancelAll={cancelAll}
        cancelling={cancelling}
        onRemoveAll={removeAll}
        removing={removing}
      />
      {transfers.length === 0 
        ? <PlaceholderSegment icon={direction} caption={`No ${direction}s to display`}/>
        : transfers.map((user, index) => 
          <TransferGroup 
            key={index} 
            direction={direction} 
            user={user}
            retry={retry}
            retryAll={retryAll}
            cancel={cancel}
            cancelAll={cancelAll}
            remove={remove}
            removeAll={removeAll}
          />
        )}
    </>
  );
}

export default Transfers;