import React, { useState, useEffect } from 'react';
import { toast } from 'react-toastify';
import { orderBy } from 'lodash';

import * as sharesLib from '../../../lib/shares';
import { 
  CodeEditor,
  LoaderSegment, 
  PlaceholderSegment, 
  ShrinkableButton, 
  Switch, 
} from '../../Shared';

import Share from './Share';

import {
  Modal,
  Button,
  Divider,
  Table,
  Icon,
} from 'semantic-ui-react';
import { Link } from 'react-router-dom';

const Index = ({ state }) => {
  const [loading, setLoading] = useState(true);
  const [shares, setShares] = useState([]);
  const [modal, setModal] = useState(false);

  const { filling, filled, faulted, fillProgress, directories, files, excludedDirectories } = state;

  useEffect(() => {
    getAll();
  }, []);

  useEffect(() => {
    if (filled) {
      getAll();
    }
  }, [filled]);

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

  const browse = async (share) => {
    const { id, localPath, remotePath } = share;
    const contents = await sharesLib.browse({ id });

    var directories = contents.map(directory => {
      const lines = [directory.name.replace(remotePath, localPath)];

      orderBy(directory.files, 'filename').forEach(file => {
        console.log(file);
        lines.push('\t' + file.filename.replace(remotePath, ''));
      });

      lines.push('');

      return lines.join('\n');
    });

    setModal({ id, localPath, remotePath, contents: directories.join('\n') });
  };

  const shared = shares.filter(share => !share.isExcluded);
  const excluded = shares.filter(share => share.isExcluded);

  return (
    <>
      <div className="header-buttons">
        <ShrinkableButton
          primary
          icon='refresh'
          loading={filling}
          disabled={filling}
          mediaQuery={'(max-width: 516px)'} 
          onClick={() => sharesLib.rescan()}
        >{filling ? 'Scanning shares' : 'Rescan Shares'}</ShrinkableButton>
      </div>
      <Divider/>
      <Switch
        filling={(loading || filling) && <LoaderSegment caption={fillProgress}/>}
      >
        <Table size='large'>
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>Local Path</Table.HeaderCell>
              <Table.HeaderCell style={{width: 110}}>Directories</Table.HeaderCell>
              <Table.HeaderCell style={{width: 110}}>Files</Table.HeaderCell>
              <Table.HeaderCell>Alias</Table.HeaderCell>
              <Table.HeaderCell>Remote Path</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {shared.map((share, index) => (<Table.Row key={index}>
              <Table.Cell onClick={() => browse(share)}>
                <Icon name='folder'/>  
                <Link to='#'>{share.localPath}</Link>
              </Table.Cell>
              <Table.Cell>{share.directories}</Table.Cell>
              <Table.Cell>{share.files}</Table.Cell>
              <Table.Cell>{share.alias}</Table.Cell>
              <Table.Cell>{share.remotePath}</Table.Cell>
            </Table.Row>))}
          </Table.Body>
        </Table>
        <Table size='large'>
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>Excluded Paths</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {excluded.map((share, index) => (<Table.Row key={index}>
              <Table.Cell><Icon name='x' color='red'/>{share.localPath}</Table.Cell>
            </Table.Row>))}
          </Table.Body>
        </Table>
      </Switch>
      <Modal
        size='large'
        open={modal}
        onClose={() => setModal(false)}
      >
        <Modal.Header><Icon name='folder'/>{modal.localPath}</Modal.Header>
        <Modal.Content scrolling className='share-ls-content'>
          <CodeEditor
            style={{minHeight: 500}}
            value={modal.contents}
            basicSetup={false}
            editable={false}
          />
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={() => setModal(false)}>
            Close
          </Button>
        </Modal.Actions>
      </Modal>
    </>    
  );
};

export default Index;