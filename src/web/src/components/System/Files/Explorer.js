import React, { useState, useEffect } from 'react';

import {
  Table,
  Icon,
  Message
} from 'semantic-ui-react';

import { list } from '../../../lib/files';
import { formatBytes, formatDate } from '../../../lib/util';
import { LoaderSegment } from '../../Shared';

const Explorer = ({ root, isActive }) => {
  const [directory, setDirectory] = useState({ files: [], directories: [] });
  const [subdirectory, setSubdirectory] = useState([]);
  const [loading, setLoading] = useState(false);
 
  useEffect(() => {
    setLoading(true);

    console.log('fetching...', subdirectory);
    const fetch = async () => {
      const dir = await list({ root, subdirectory: subdirectory.join('/') });
      console.log(dir)
      setDirectory(dir);
      setLoading(false);
    };

    fetch();
  }, [isActive, subdirectory]);

  const select = ({ path }) => {
    setSubdirectory([...subdirectory, path]);
    console.log(path)
  };

  const row = (icon, { name, fullName, modifiedAt, length }, onClick = () => { }) => <Table.Row key={name}>
    <Table.Cell
      style={{ cursor: 'pointer' }}
      onClick={() => onClick({ path: name })}
    ><Icon name={icon} />{name}</Table.Cell>
    <Table.Cell>{modifiedAt ? formatDate(modifiedAt) : ''}</Table.Cell>
    <Table.Cell>{length ? formatBytes(length) : ''}</Table.Cell>
    <Table.Cell>
      <Icon name="trash alternate" color="red" onClick={() => console.log(fullName)} style={{ cursor: 'pointer' }}/>
    </Table.Cell>
  </Table.Row>;

  if (loading) {
    return <LoaderSegment/>;
  }

  return (
    <>
      {!!subdirectory && <Message className='no-grow edit-code-header'>
        <Icon name='folder open'/>{root + '/' + subdirectory}
      </Message>}
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
          {!!subdirectory && row('folder', { name: '..', fullName: '..' })}
          {directory?.directories?.map((d => row('folder', d, select)))}
          {directory?.files?.map(f => row('file outline', f))}
        </Table.Body>
      </Table>
    </>
  );
};

export default Explorer;