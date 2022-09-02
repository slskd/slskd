import React, { useState, useEffect } from 'react';
import { toast } from 'react-toastify';

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
  const [shares, setShares] = useState([]);
  const [modal, setModal] = useState(false);

  const { scanning, scanProgress, scanPending } = state;
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
      setShares(await sharesLib.getAll());
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setLoading(false);
    }
  };

  const shared = shares.filter(share => !share.isExcluded);
  const excluded = shares.filter(share => share.isExcluded);

  return (
    <>
      <div className="header-buttons">
        <ShrinkableButton
          primary={!scanPending}
          color={scanPending ? 'yellow' : undefined}
          icon='refresh'
          loading={scanning}
          disabled={scanning || loading}
          mediaQuery={'(max-width: 516px)'} 
          onClick={() => sharesLib.rescan()}
        >{scanning ? 'Scanning shares' : 'Rescan Shares'}</ShrinkableButton>
      </div>
      <Divider/>
      <Switch
        filling={(loading || scanning) && <LoaderSegment>{Math.round(scanProgress * 100)}%</LoaderSegment>}
      >
        <ShareTable shares={shared} onClick={setModal}/>
        <ExclusionTable exclusions={excluded}/>
      </Switch>
      <ContentsModal share={modal} onClose={() => setModal(false)}/>
    </>    
  );
};

export default Index;