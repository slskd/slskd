import React, { useState, useEffect } from 'react';
import { toast } from 'react-toastify';

import * as sharesLib from '../../../lib/shares';
import { LoaderSegment, PlaceholderSegment, ShrinkableButton, Switch } from '../../Shared';
import Share from './Share';

import {
  Modal,
  Button,
  Divider,
} from 'semantic-ui-react';

const Index = ({ state }) => {
  const [loading, setLoading] = useState(true);
  const [shares, setShares] = useState([]);
  const [everything, setEverything] = useState();
  const [modal, setModal] = useState(false);

  useEffect(() => {
    getAll();
  }, []);

  const getAll = async () => {
    try {
      setLoading(true);
      setShares(await sharesLib.getAll());
      setLoading(false);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setLoading(false);
    }
  };

  const browse = async ({ id }) => {
    setEverything(await sharesLib.browse({ id }));
    setModal(true);
  };

  const rescan = async () => {
    await sharesLib.rescan();
  };

  if (loading) {
    return LoaderSegment;
  }

  const { filling, filled, faulted, fillProgress, directories, files, excludedDirectories } = state;

  return (
    <>
      <div className="header-buttons">
        <ShrinkableButton
          primary
          icon='refresh'
          loading={filling}
          disabled={filling}
          mediaQuery={'(max-width: 516px)'} 
          onClick={rescan}
        >{filling ? 'Scanning shares' : 'Rescan Shares'}</ShrinkableButton>
      </div>
      <Divider/>
      <Switch
        filling={filling && <PlaceholderSegment loading caption={fillProgress}/>}
      >
        <div className='view-code-container'>
          <ol>
            {shares.map(share => (
              <Share share={share} onBrowse={() => browse({ id: share.id })}/>
            ))}
          </ol>
          <pre>
            {JSON.stringify(state, null, 2)}
          </pre>
        </div>
      </Switch>
      <Modal
        size='large'
        open={modal}
        onClose={() => setModal(false)}
        style={{ color: 'red' }}
      >
        <Modal.Header>Delete Your Account</Modal.Header>
        <Modal.Content scrolling>
          <pre>
            {JSON.stringify(everything, null, 2)}
          </pre>
        </Modal.Content>
        <Modal.Actions>
          <Button negative onClick={() => setModal(false)}>
            No
          </Button>
          <Button positive onClick={() => setModal(false)}>
            Yes
          </Button>
        </Modal.Actions>
      </Modal>
    </>    
  );
};

export default Index;