import React, { useState, useEffect } from 'react';
import { toast } from 'react-toastify';
import { flatten } from 'lodash';

import * as sharesLib from '../../../lib/shares';
import { 
  LoaderSegment,
  ShrinkableButton, 
  Switch, 
} from '../../Shared';

import ContentsModal from './ContentsModal';

import {
  Divider,
} from 'semantic-ui-react';

import ExclusionTable from './ExclusionTable';
import ShareTable from './ShareTable';

const Index = ({ state = {} } = {}) => {
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [shares, setShares] = useState([]);
  const [modal, setModal] = useState(false);

  const { scanning, scanProgress, scanPending, directories, files } = state;
  const scanned = !scanning;

  useEffect(() => {
    getAll();
  }, []);

  useEffect(() => {
    if (scanned) {
      getAll();
    }
  }, [scanned]);

  const getAll = async () => {
    try {
      setLoading(true);

      const sharesByHost = await sharesLib.getAll();
      const flattened = Object.entries(sharesByHost).reduce((acc, [host, shares]) => {
        return acc.concat(shares.map(share => ({ host, ...share })));
      }, []);

      setShares(flattened);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setLoading(false);
    }
  };

  const rescan = async () => {
    try {
      setWorking(true);
      await sharesLib.rescan();
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setWorking(false);
    }
  };

  const cancel = async () => {
    try {
      setWorking(true);
      await sharesLib.cancel();
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error ?? 'Failed to cancel the scan');
    } finally {
      setWorking(false);      
    }
  };

  const ScanButton = () => <ShrinkableButton
    primary={!scanPending}
    color={scanPending ? 'yellow' : undefined}
    icon='refresh'
    loading={working}
    disabled={working}
    mediaQuery={'(max-width: 516px)'} 
    onClick={() => rescan()}
  >Rescan Shares</ShrinkableButton>;

  const CancelButton = () => <ShrinkableButton
    color='red'
    icon='x'
    disabled={working}
    mediaQuery={'(max-width: 516px)'}
    onClick={() => cancel()}
  >Cancel Scan</ShrinkableButton>;

  const shared = shares.filter(share => !share.isExcluded);
  const excluded = shares.filter(share => share.isExcluded);

  return (
    <>
      <Switch
        loading={loading && <LoaderSegment/>}
      >
        <div className="header-buttons">
          <Switch
            scanning={scanning && <CancelButton/>}
          >
            <ScanButton/>
          </Switch>
        </div>
        <Divider/>
        <Switch
          filling={scanning && <LoaderSegment>
            <div>
              <div>{Math.round(scanProgress * 100)}%</div>
              <div className='share-scan-detail'>Found {files} files in {directories} directories</div>
            </div></LoaderSegment>}
        >
          <ShareTable shares={shared} onClick={setModal}/>
          <ExclusionTable exclusions={excluded}/>
        </Switch>
        <ContentsModal share={modal} onClose={() => setModal(false)}/>
      </Switch>
    </>    
  );
};

export default Index;