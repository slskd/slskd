import '../Files/Files.css';
import { getFileBlob, list } from '../../../lib/logs';
import { downloadFile, formatBytes, formatDate } from '../../../lib/util';
import { LoaderSegment } from '../../Shared';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import { Header, Icon, Table } from 'semantic-ui-react';

const view = async (filename) => {
  const tab = window.open('', '_blank');

  try {
    const blob = await getFileBlob({ filename });

    // hack: we can't pass a JWT in a plain link, so we have to fetch the blob
    // and then shove it into the window rather than just displaying it
    tab.location.href = window.URL.createObjectURL(
      new Blob([blob], { type: 'text/plain; charset=utf-8' }),
    );
  } catch (error) {
    tab.close();
    toast.error(error?.message ?? error);
  }
};

const download = async (filename) => {
  try {
    const blob = await getFileBlob({ filename });

    downloadFile(blob, filename, 'text/plain');
  } catch (error) {
    toast.error(error?.message ?? error);
  }
};

const FileRow = ({ length, modifiedAt, name }) => (
  <Table.Row key={name}>
    <Table.Cell>
      <Icon name="file alternate outline" />
      {name}
    </Table.Cell>
    <Table.Cell>{modifiedAt ? formatDate(modifiedAt) : ''}</Table.Cell>
    <Table.Cell>{length ? formatBytes(length) : ''}</Table.Cell>
    <Table.Cell style={{ whiteSpace: 'nowrap' }}>
      <Icon
        name="external alternate"
        onClick={() => view(name)}
        style={{ cursor: 'pointer', marginRight: '0.5em' }}
        title={`View ${name} in a new window`}
      />
      <Icon
        name="download"
        onClick={() => download(name)}
        style={{ cursor: 'pointer' }}
        title={`Download ${name}`}
      />
    </Table.Cell>
  </Table.Row>
);

const Explorer = () => {
  const [files, setFiles] = useState([]);
  const [loading, setLoading] = useState(false);
  const [sortColumn, setSortColumn] = useState('date');
  const [sortDirection, setSortDirection] = useState('descending');

  const fetch = async () => {
    setLoading(true);

    try {
      setFiles(await list());
    } catch (error) {
      toast.error(error?.message ?? error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetch();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

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

  const sortedFiles = sortItems(files);

  return (
    <>
      <Header
        className="explorer-working-directory"
        size="small"
      >
        <Icon name="folder open" />
        /logs
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
          {files.length === 0 ? (
            <Table.Row>
              <Table.Cell
                colSpan={99}
                style={{
                  opacity: 0.5,
                  padding: '10px !important',
                  textAlign: 'center',
                }}
              >
                No log files
              </Table.Cell>
            </Table.Row>
          ) : (
            sortedFiles.map((f) => (
              <FileRow
                key={f.name}
                {...f}
              />
            ))
          )}
        </Table.Body>
      </Table>
    </>
  );
};

export default Explorer;
