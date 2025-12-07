import { deleteDirectory, deleteFile, list } from '../../../lib/files';
import { formatBytes, formatDate } from '../../../lib/util';
import { LoaderSegment } from '../../Shared';
import React, { useEffect, useState } from 'react';
import { Header, Icon, Modal, Table } from 'semantic-ui-react';

const FileRow = ({
  fullName,
  length,
  modifiedAt,
  name,
  remoteFileManagement,
  root,
  subdirectory,
}) => (
  <Table.Row key={fullName}>
    <Table.Cell>
      <Icon name="file outline" />
      {name}
    </Table.Cell>
    <Table.Cell>{modifiedAt ? formatDate(modifiedAt) : ''}</Table.Cell>
    <Table.Cell>{length ? formatBytes(length) : ''}</Table.Cell>
    <Table.Cell>
      {remoteFileManagement ? (
        <Modal
          actions={[
            'Cancel',
            {
              content: 'Delete',
              key: 'done',
              negative: true,
              onClick: async () => {
                await deleteFile({
                  path: `${subdirectory.join('/')}/${fullName}`,
                  root,
                });
                fetch();
              },
            },
          ]}
          centered
          content={`Are you sure you want to delete file '${fullName}'?`}
          header={
            <Header
              content="Confirm File Delete"
              icon="trash alternate"
            />
          }
          size="small"
          trigger={
            <Icon
              color="red"
              name="trash alternate"
              style={{ cursor: 'pointer' }}
            />
          }
        />
      ) : null}
    </Table.Cell>
  </Table.Row>
);

const DirectoryRow = ({
  deletable = true,
  fullName,
  modifiedAt,
  name,
  onClick = () => {},
  remoteFileManagement,
  root,
  subdirectory,
}) => (
  <Table.Row key={name}>
    <Table.Cell
      onClick={onClick}
      style={{ cursor: 'pointer' }}
    >
      <Icon name="folder" />
      {name}
    </Table.Cell>
    <Table.Cell>{modifiedAt ? formatDate(modifiedAt) : ''}</Table.Cell>
    <Table.Cell />
    <Table.Cell>
      {remoteFileManagement && deletable ? (
        <Modal
          actions={[
            'Cancel',
            {
              content: 'Delete',
              key: 'done',
              negative: true,
              onClick: async () => {
                await deleteDirectory({
                  path: `${subdirectory.join('/')}/${fullName}`,
                  root,
                });
                fetch();
              },
            },
          ]}
          centered
          content={`Are you sure you want to delete directory '${fullName}'?`}
          header={
            <Header
              content="Confirm Directory Delete"
              icon="trash alternate"
            />
          }
          size="small"
          trigger={
            <Icon
              color="red"
              name="trash alternate"
              style={{ cursor: 'pointer' }}
            />
          }
        />
      ) : (
        ''
      )}
    </Table.Cell>
  </Table.Row>
);

const Explorer = ({ remoteFileManagement, root }) => {
  const [directory, setDirectory] = useState({ directories: [], files: [] });
  const [subdirectory, setSubdirectory] = useState([]);
  const [loading, setLoading] = useState(false);
  const [sortColumn, setSortColumn] = useState('name');
  const [sortDirection, setSortDirection] = useState('ascending');

  const fetch = async () => {
    setLoading(true);
    const directoryResult = await list({
      root,
      subdirectory: subdirectory.join('/'),
    });
    setDirectory(directoryResult);
    setLoading(false);
  };

  useEffect(() => {
    fetch();
  }, [subdirectory]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    setSubdirectory([]);
  }, [root]);

  const select = ({ path }) => {
    setSubdirectory([...subdirectory, path]);
  };

  const upOneSubdirectory = () => {
    const copy = [...subdirectory];
    copy.pop();
    setSubdirectory(copy);
  };

  const handleSort = (column) => {
    if (sortColumn === column) {
      setSortDirection(
        sortDirection === 'ascending' ? 'descending' : 'ascending',
      );
    } else {
      setSortColumn(column);
      setSortDirection('ascending');
    }
  };

  const sortItems = (items) => {
    if (!items || items.length === 0) return items;

    return [...items].sort((a, b) => {
      let compareValue = 0;

      if (sortColumn === 'name') {
        compareValue = a.name.localeCompare(b.name, undefined, {
          numeric: true,
          sensitivity: 'base',
        });
      } else if (sortColumn === 'date') {
        const dateA = a.modifiedAt ? new Date(a.modifiedAt) : new Date(0);
        const dateB = b.modifiedAt ? new Date(b.modifiedAt) : new Date(0);
        compareValue = dateA - dateB;
      }

      return sortDirection === 'ascending' ? compareValue : -compareValue;
    });
  };

  if (loading) {
    return <LoaderSegment />;
  }

  const total = directory?.directories?.length + directory?.files?.length ?? 0;
  const sortedDirectories = sortItems(directory?.directories);
  const sortedFiles = sortItems(directory?.files);

  return (
    <>
      <Header
        className="explorer-working-directory"
        size="small"
      >
        <Icon name="folder open" />
        {'/' + root + '/' + subdirectory.join('/')}
      </Header>
      <Table
        className="unstackable"
        size="large"
      >
        <Table.Header>
          <Table.Row>
            <Table.HeaderCell
              className="explorer-list-name"
              onClick={() => handleSort('name')}
              style={{ cursor: 'pointer' }}
            >
              Name
              {sortColumn === 'name' && (
                <Icon
                  name={
                    sortDirection === 'ascending'
                      ? 'chevron up'
                      : 'chevron down'
                  }
                />
              )}
            </Table.HeaderCell>
            <Table.HeaderCell
              className="explorer-list-date"
              onClick={() => handleSort('date')}
              style={{ cursor: 'pointer' }}
            >
              Date Modified
              {sortColumn === 'date' && (
                <Icon
                  name={
                    sortDirection === 'ascending'
                      ? 'chevron up'
                      : 'chevron down'
                  }
                />
              )}
            </Table.HeaderCell>
            <Table.HeaderCell className="explorer-list-size">
              Size
            </Table.HeaderCell>
            <Table.HeaderCell className="explorer-list-action" />
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {total === 0 ? (
            <Table.Row>
              <Table.Cell
                colSpan={99}
                style={{
                  opacity: 0.5,
                  padding: '10px !important',
                  textAlign: 'center',
                }}
              >
                No files or directories
              </Table.Cell>
            </Table.Row>
          ) : (
            <>
              {subdirectory.length > 0 && (
                <DirectoryRow
                  deletable={false}
                  fullName=".."
                  name=".."
                  onClick={upOneSubdirectory}
                  remoteFileManagement={remoteFileManagement}
                  root={root}
                  subdirectory={subdirectory}
                />
              )}
              {sortedDirectories?.map((d) => (
                <DirectoryRow
                  key={d.name}
                  onClick={() => select({ path: d.name })}
                  remoteFileManagement={remoteFileManagement}
                  root={root}
                  subdirectory={subdirectory}
                  {...d}
                />
              ))}
              {sortedFiles?.map((f) => (
                <FileRow
                  key={f.name}
                  remoteFileManagement={remoteFileManagement}
                  root={root}
                  subdirectory={subdirectory}
                  {...f}
                />
              ))}
            </>
          )}
        </Table.Body>
      </Table>
    </>
  );
};

export default Explorer;
