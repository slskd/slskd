import { browse } from '../../../lib/shares';
import { CodeEditor, LoaderSegment, Switch } from '../../Shared';
import { orderBy } from 'lodash';
import React, { useEffect, useState } from 'react';
import { Button, Icon, Modal } from 'semantic-ui-react';

const ContentsModal = ({ onClose, share, theme }) => {
  const [loading, setLoading] = useState(true);
  const [contents, setContents] = useState();

  const { id, localPath, remotePath } = share || {};

  useEffect(() => {
    const fetch = async () => {
      setLoading(true);

      const result = await browse({ id });

      const directories = result.map((directory) => {
        const lines = [directory.name.replace(remotePath, localPath)];

        for (const file of orderBy(directory.files, 'filename')) {
          lines.push('\t' + file.filename.replace(remotePath, ''));
        }

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
      onClose={onClose}
      open={share}
      size="large"
    >
      <Modal.Header>
        <Icon name="folder" />
        {localPath}
      </Modal.Header>
      <Modal.Content
        className="share-ls-content"
        scrolling
      >
        <Switch loading={loading && <LoaderSegment className="modal-loader" />}>
          <CodeEditor
            basicSetup={false}
            editable={false}
            style={{ minHeight: 500 }}
            theme={theme}
            value={contents || ''}
          />
        </Switch>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={onClose}>Close</Button>
      </Modal.Actions>
    </Modal>
  );
};

export default ContentsModal;
