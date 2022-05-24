import React, { useState, useEffect } from 'react';
import { getAll } from '../../lib/transfers';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import { toast } from 'react-toastify';

import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';

import './Transfers.css';

const Transfers = ({ direction, server }) => {
  const [transfers, setTransfers] = useState([]);
  const [refreshInterval, setRefreshInterval] = useState(undefined);

  const [fetching, setFetching] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [removing, setRemoving] = useState(false);

  useEffect(() => {
    const fetch = async () => {
      try {
        setFetching(true);
        const response = await getAll({ direction });
        setTransfers(response);
        setFetching(false);

        setRefreshInterval(window.setInterval(fetch, 500));
      } catch (error) {
        console.error(error);
        toast.error(error?.response?.data ?? error?.message ?? error);
        setFetching(false);
      }
    }

    fetch();

    return () => {
      clearInterval(refreshInterval);
      setRefreshInterval(undefined);
    }
  }, []);

  const retry = async (file) => {
    const { username, filename, size } = file;
        
    try {
      setRetrying(true);
      await transfers.download({username, files: [{filename, size }] });
      setRetrying(false);
    } catch (error) {
      console.log(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRetrying(false);
    }
  }

  const retryAll = async (transfers) => {
    console.log(transfers);
    // await Promise.all(transfers.map(file => this.retry(file)))
  }

  const cancel = async (file) => {
    try {
      setCancelling(true);
      setCancelling(false);
    } catch (error) {
      console.log(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setCancelling(false);
    }
  }

  const cancelAll = async (transfers) => {
    console.log(transfers);
  }

  const remove = async (file) => {
    try {
      setRemoving(true);
      setRemoving(false);
    } catch (error) {
      console.log(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRemoving(false);
    }
  }

  const removeAll = async (transfers) => {
    console.log(transfers);
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
          <TransferGroup key={index} direction={this.props.direction} user={user}/>
        )}
    </>
  );
}

export default Transfers;