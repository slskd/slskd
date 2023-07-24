import React, { useState, useEffect } from 'react';

import {
  Table,
  Icon,
  Message,
  Modal,
  Header,
} from 'semantic-ui-react';

import { list, deleteDirectory, deleteFile } from '../../../lib/files';
import { formatBytes, formatDate } from '../../../lib/util';
import { LoaderSegment } from '../../Shared';

const Explorer = ({ root }) => {
  const [directory, setDirectory] = useState({ files: [], directories: [] });
  const [subdirectory, setSubdirectory] = useState([]);
  const [loading, setLoading] = useState(false);
 
  useEffect(() => {
    fetch();
  }, [subdirectory]); // eslint-disable-line react-hooks/exhaustive-deps
  
  useEffect(() => {
    setSubdirectory([]);
  }, [root]);

  const fetch = async () => {
    setLoading(true);
    const dir = await list({ root, subdirectory: subdirectory.join('/') });
    setDirectory(dir);
    setLoading(false);
  };

  const select = ({ path }) => {
    setSubdirectory([...subdirectory, path]);
  };

  const upOneSubdirectory = () => {
    const copy = [...subdirectory];
    copy.pop();
    setSubdirectory(copy);
  };

  const FileRow = ({ name, fullName, modifiedAt, length }) => <Table.Row key={fullName}>
    <Table.Cell><Icon name='file outline'/>{name}</Table.Cell>
    <Table.Cell>{modifiedAt ? formatDate(modifiedAt) : ''}</Table.Cell>
    <Table.Cell>{length ? formatBytes(length) : ''}</Table.Cell>
    <Table.Cell>
      <Modal
        trigger={
          <Icon name="trash alternate" color="red" style={{ cursor: 'pointer' }}/>
        }
        centered
        size='small'
        header={<Header icon='trash alternate' content='Confirm File Delete' />}
        content={`Are you sure you want to delete file '${fullName}'?`}
        actions={[
          'Cancel',
          {
            key: 'done',
            content: 'Delete',
            negative: true,
            onClick: async () => {
              await deleteFile({ root, path: `${subdirectory.join('/')}/${fullName}`});
              fetch();
            },
          },
        ]}
      />
    </Table.Cell>
  </Table.Row>;

  const DirectoryRow = ({ name, fullName, modifiedAt, onClick = () => {} }) => <Table.Row key={fullName}>
    <Table.Cell
      style={{ cursor: 'pointer' }}
      onClick={onClick}
    ><Icon name='folder'/>{name}</Table.Cell>
    <Table.Cell>{modifiedAt ? formatDate(modifiedAt) : ''}</Table.Cell>
    <Table.Cell></Table.Cell>
    <Table.Cell>
      <Modal
        trigger={
          <Icon name="trash alternate" color="red" style={{ cursor: 'pointer' }}/>
        }
        centered
        size='small'
        header={<Header icon='trash alternate' content='Confirm Directory Delete' />}
        content={`Are you sure you want to delete directory '${fullName}'?`}
        actions={[
          'Cancel',
          {
            key: 'done',
            content: 'Delete',
            negative: true,
            onClick: async () => {
              await deleteDirectory({ root, path: `${subdirectory.join('/')}/${fullName}`});
              fetch();
            },
          },
        ]}
      />
    </Table.Cell>  
  </Table.Row>;

  if (loading) {
    return <LoaderSegment/>;
  }

  const total = directory?.directories?.length + directory?.files?.length ?? 0;

  return (
    <>
      <Header size='small' className='explorer-working-directory'>
        <Icon name='folder open'/>{'/' + root + '/' + subdirectory.join('/')}
      </Header>
      <Table size='large' className='unstackable'>
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell className="">Name</Table.HeaderCell>
            <Table.HeaderCell className="">Date Modified</Table.HeaderCell>
            <Table.HeaderCell className="">Size</Table.HeaderCell>
            <Table.HeaderCell className="explorer-list-action"></Table.HeaderCell>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {total === 0
            ? <Table.Row>
              <Table.Cell colSpan={99} style={{ opacity: .5, padding: '10px !important', textAlign: 'center' }}>
                No files or directories
              </Table.Cell>
            </Table.Row>
            : <>
              {subdirectory.length > 0 && <DirectoryRow name=".." fullName=".." onClick={upOneSubdirectory}/>}
              {directory?.directories?.map(d => <DirectoryRow onClick={() => select({ path: d.name })} {...d}/>)}
              {directory?.files?.map(f => <FileRow {...f}/>)}
            </>
          }
        </Table.Body>
      </Table>
    </>
  );
};

export default Explorer;