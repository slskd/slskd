import React, { useEffect, useState } from 'react';
import { orderBy } from 'lodash';

import { browse } from '../../../lib/shares';

import {
  Modal,
  Button,
  Icon,
} from 'semantic-ui-react';

import {
  CodeEditor, LoaderSegment, Switch,
} from '../../Shared';

const ContentsModal = ({ share, onClose, theme }) => {
  const [loading, setLoading] = useState(true);
  const [contents, setContents] = useState();

  const { id, localPath, remotePath } = share || {};

  useEffect(() => {
    const fetch = async () => {
      setLoading(true);
      
      const contents = await browse({ id });
  
      var directories = contents.map(directory => {
        const lines = [directory.name.replace(remotePath, localPath)];
    
        orderBy(directory.files, 'filename').forEach(file => {
          lines.push('\t' + file.filename.replace(remotePath, ''));
        });
    
        lines.push('');
    
        return lines.join('\n');
      });
    
      setContents(directories.join('\n'));
      setLoading(false);
    };

    if (id) {
      fetch();
    } else {
      setLoading(true);
      setContents();
    }
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <Modal
      size='large'
      open={share}
      onClose={onClose}
    >
      <Modal.Header><Icon name='folder'/>{localPath}</Modal.Header>
      <Modal.Content scrolling className='share-ls-content'>
        <Switch
          loading={loading && <LoaderSegment className="modal-loader"/>}
        >
          <CodeEditor
            style={{minHeight: 500}}
            value={contents || ''}
            basicSetup={false}
            editable={false}
            theme={theme}
          />
        </Switch>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={onClose}>
          Close
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default ContentsModal;