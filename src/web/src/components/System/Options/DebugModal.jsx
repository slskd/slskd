import { getCurrentDebugView } from '../../../lib/options';
import { CodeEditor, PlaceholderSegment, Switch } from '../../Shared';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import { Button, Icon, Modal } from 'semantic-ui-react';

const DebugModal = ({ onClose, open, theme }) => {
  const [loading, setLoading] = useState(true);
  const [debugView, setDebugView] = useState();

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

  useEffect(() => {
    if (open) {
      get();
    }
  }, [open]);

  return (
    <Modal
      onClose={onClose}
      open={open}
      size="large"
    >
      <Modal.Header>
        <Icon name="bug" />
        Options (Debug View)
      </Modal.Header>
      <Modal.Content
        className="debug-view-content"
        scrolling
      >
        <Switch loading={loading && <PlaceholderSegment loading />}>
          <CodeEditor
            basicSetup={false}
            editable={false}
            style={{ minHeight: 500 }}
            theme={theme}
            value={debugView}
          />
        </Switch>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={onClose}>Close</Button>
      </Modal.Actions>
    </Modal>
  );
};

export default DebugModal;
