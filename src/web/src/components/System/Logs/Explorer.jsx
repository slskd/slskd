import '../Files/Files.css';
import { getFileBlob, list } from '../../../lib/logs';
import { downloadFile, formatBytes, formatDate } from '../../../lib/util';
import {
  ErrorSegment,
  LoaderSegment,
  PlaceholderSegment,
  Switch,
} from '../../Shared';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import { Header, Icon, Table } from 'semantic-ui-react';

const view = async (filename) => {
  const tab = window.open('', '_blank');

  if (!tab) {
    toast.error(
      'Unable to open a new window; allow pop-ups for this site to view logs',
    );
    return;
  }

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
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(undefined);
  const [sortColumn, setSortColumn] = useState('date');
  const [sortDirection, setSortDirection] = useState('descending');

  const fetch = async () => {
    setLoading(true);
    setError(undefined);

    try {
      setFiles(await list());
    } catch (fetchError) {
      if (fetchError?.response?.status === 404) {
        setFiles([]);
      } else {
        setError(fetchError?.message ?? fetchError);
      }
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
      <Switch
        empty={
          !loading &&
          !error &&
          files.length === 0 && (
            <PlaceholderSegment
              caption="No log files"
              icon="file alternate outline"
            />
          )
        }
        error={error && <ErrorSegment caption={error} />}
        loading={loading && <LoaderSegment />}
      >
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
            {sortedFiles.map((f) => (
              <FileRow
                key={f.name}
                {...f}
              />
            ))}
          </Table.Body>
        </Table>
      </Switch>
    </>
  );
};

export default Explorer;
