import React, { useState, useEffect } from 'react';
import { toast } from 'react-toastify';

import { 
  Modal, 
  Button,
  Icon, 
} from 'semantic-ui-react';

import { getCurrentDebugView } from '../../../lib/options';
import { CodeEditor, PlaceholderSegment, Switch } from '../../Shared';

const DebugModal = ({ open, onClose }) => {
  const [loading, setLoading] = useState(true);
  const [debugView, setDebugView] = useState();

  useEffect(() => {
    if (open) {
      get();
    }
  }, [open]);

  const get = async () => {
    setLoading(true);

    try {
      setDebugView(await getCurrentDebugView());
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      setLoading(false);
    }
  };
  
  return (      
    <Modal
      size='large'
      open={open}
      onClose={onClose}
    >
      <Modal.Header>
        <Icon name='bug'/>
        Options (Debug View)
      </Modal.Header>
      <Modal.Content className='debug-view-content' scrolling>
        <Switch
          loading={loading && <PlaceholderSegment loading={true} />}
        >
          <div className='view-code-container'>
            <CodeEditor
              value={debugView}
              basicSetup={false}
              editable={false}
            />
          </div>
        </Switch>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={onClose}>
          Close
        </Button>
      </Modal.Actions>
    </Modal>);
};

export default DebugModal;