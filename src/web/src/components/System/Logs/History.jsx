import './Logs.css';
import '../System.css';
import { getLogFile, getLogFiles } from '../../../lib/logs';
import { ErrorSegment, LoaderSegment } from '../../Shared';
import Controls, { defaultPerPage, levels } from './Controls';
import FileNavigator from './FileNavigator';
import LogTable from './LogTable';
import React, { useEffect, useMemo, useState } from 'react';
import { Pagination } from 'semantic-ui-react';

/**
 * Sorts log files newest first, so that the most recent is selected by default
 * and sits at the top of the file dropdown.
 */
const newestFirst = (files) =>
  [...files].sort((a, b) => {
    const dateA = a.modifiedAt ? new Date(a.modifiedAt) : new Date(0);
    const dateB = b.modifiedAt ? new Date(b.modifiedAt) : new Date(0);

    return dateB - dateA;
  });

const History = () => {
  const [files, setFiles] = useState([]);
  const [file, setFile] = useState(undefined);
  const [logs, setLogs] = useState([]);
  const [loadingFiles, setLoadingFiles] = useState(true);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const [error, setError] = useState(undefined);

  const [level, setLevel] = useState(levels[0]);
  const [filter, setFilter] = useState('');
  const [sortDirection, setSortDirection] = useState('descending');
  const [page, setPage] = useState(1);
  const [perPage, setPerPage] = useState(defaultPerPage);

  // fetch the list of files on mount, and select the most recent
  useEffect(() => {
    const fetch = async () => {
      setLoadingFiles(true);

      try {
        const sorted = newestFirst(await getLogFiles());

        setFiles(sorted);
        setFile(sorted[0]?.name);
      } catch (fetchError) {
        setError(fetchError);
      } finally {
        setLoadingFiles(false);
      }
    };

    fetch();
  }, []);

  // load the contents of the selected file whenever the selection changes
  useEffect(() => {
    if (!file) {
      return;
    }

    const fetch = async () => {
      setLoadingLogs(true);
      setError(undefined);

      try {
        const records = await getLogFile({ filename: file });

        setLogs(records);
      } catch (fetchError) {
        setError(fetchError);
        setLogs([]);
      } finally {
        setLoadingLogs(false);
      }
    };

    fetch();
  }, [file]);

  // apply the level filter, text filter, and sort.  log files can be large, so
  // memoize this to keep typing in the filter responsive
  const filteredLogs = useMemo(() => {
    const minimumSeverity = levels.indexOf(level);
    const text = filter.trim().toLowerCase();

    return logs
      .filter((log) => levels.indexOf(log.level) >= minimumSeverity)
      .filter(
        (log) =>
          !text ||
          log.message?.toLowerCase().includes(text) ||
          log.level?.toLowerCase().includes(text),
      )
      .sort((a, b) => {
        // timestamps are only second precision, so fall back to position in the
        // file to keep lines logged within the same second in the right order
        const compareValue =
          new Date(a.timestamp) - new Date(b.timestamp) || a.id - b.id;

        return sortDirection === 'ascending' ? compareValue : -compareValue;
      });
  }, [filter, level, logs, sortDirection]);

  const totalPages = Math.ceil(filteredLogs.length / perPage);

  // anything that changes the shape of the result set resets pagination, so a
  // user is never left looking at a page that no longer exists
  const currentPage = Math.min(page, Math.max(totalPages, 1));

  const pagedLogs = useMemo(
    () =>
      filteredLogs.slice((currentPage - 1) * perPage, currentPage * perPage),
    [currentPage, filteredLogs, perPage],
  );

  const changeFile = (value) => {
    setFile(value);
    setPage(1);
  };

  const changeLevel = (value) => {
    setLevel(value);
    setPage(1);
  };

  const changeFilter = (value) => {
    setFilter(value);
    setPage(1);
  };

  const changePerPage = (value) => {
    setPerPage(value);
    setPage(1);
  };

  const toggleSort = () => {
    setSortDirection(
      sortDirection === 'ascending' ? 'descending' : 'ascending',
    );
    setPage(1);
  };

  const paginationChanged = ({ activePage }) => {
    if (activePage >= 1) {
      setPage(activePage);
    }
  };

  if (loadingFiles) {
    return <LoaderSegment />;
  }

  const hiddenCount = logs.length - filteredLogs.length;
  const firstOnPage = (currentPage - 1) * perPage + 1;
  const lastOnPage = firstOnPage + pagedLogs.length - 1;

  return (
    <div className="logs">
      <Controls
        filter={filter}
        level={level}
        onFilterChange={changeFilter}
        onLevelChange={changeLevel}
        onPerPageChange={changePerPage}
        perPage={perPage}
      />
      <FileNavigator
        file={file}
        files={files}
        onChange={changeFile}
      />
      {error ? (
        <ErrorSegment caption={error?.message ?? error} />
      ) : loadingLogs ? (
        <LoaderSegment />
      ) : (
        <>
          {totalPages > 1 && (
            <div className="logs-pagination">
              <Pagination
                activePage={currentPage}
                onPageChange={(_event, data) => paginationChanged({ ...data })}
                siblingRange={1}
                totalPages={totalPages}
              />
            </div>
          )}
          <LogTable
            emptyMessage={
              files.length === 0
                ? 'No log files'
                : logs.length === 0
                  ? 'This log file is empty'
                  : 'No lines match the current filters'
            }
            logs={pagedLogs}
            onSortChange={toggleSort}
            sortDirection={sortDirection}
          />
          {filteredLogs.length > 0 && (
            <div className="logs-count">
              {`Showing ${firstOnPage}-${lastOnPage} of ${filteredLogs.length} lines` +
                (hiddenCount > 0
                  ? ` (${hiddenCount} of ${logs.length} hidden by filter(s))`
                  : '')}
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default History;
