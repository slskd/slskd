import {
  formatAttributes,
  formatBytes,
  formatSeconds,
  getFileName,
} from '../../lib/util';
import React, { useMemo, useState } from 'react';
import { Checkbox, Header, Icon, Table } from 'semantic-ui-react';

const formatDirCaption = (fileCount, dirCount) => {
  const parts = [];
  if (fileCount > 0) {
    parts.push(`${fileCount} file${fileCount === 1 ? '' : 's'}`);
  }

  if (dirCount > 0) {
    parts.push(`${dirCount} director${dirCount === 1 ? 'y' : 'ies'}`);
  }

  return parts.length > 0 ? parts.join(', ') : '0 files';
};

const AUDIO_EXTENSIONS = new Set([
  'aac',
  'aif',
  'aiff',
  'alac',
  'ape',
  'flac',
  'm4a',
  'mp3',
  'ogg',
  'opus',
  'wav',
  'wma',
]);
const VIDEO_EXTENSIONS = new Set([
  'avi',
  'flv',
  'm4v',
  'mkv',
  'mov',
  'mp4',
  'webm',
  'wmv',
]);

const getFileIcon = (filename) => {
  const ext = filename.split('.').pop()?.toLowerCase();

  if (AUDIO_EXTENSIONS.has(ext)) return 'file audio outline';
  if (VIDEO_EXTENSIONS.has(ext)) return 'file video outline';
  return 'file outline';
};

const getAttributes = (files) => {
  if (!files || files.length === 0) return '';
  if (files.every((f) => f.isVariableBitRate)) return 'VBR';
  if (files.some((f) => f.isVariableBitRate)) return 'Mixed';

  const bitRates = new Set(files.map((f) => f.bitRate).filter(Boolean));

  if (bitRates.size === 1) return `${[...bitRates][0]} Kbps`;
  if (bitRates.size > 1) return 'Mixed';
  return '';
};

const File = ({ disabled, file, indent, locked, onSelectionChange }) => (
  <Table.Row>
    <Table.Cell className="filelist-selector">
      {onSelectionChange != null && (
        <Checkbox
          checked={file.selected || false}
          disabled={disabled}
          fitted
          onChange={(_, data) => onSelectionChange(file, data.checked)}
        />
      )}
    </Table.Cell>
    <Table.Cell
      className="filelist-filename"
      style={indent ? { paddingLeft: '2em' } : undefined}
    >
      {locked ? (
        <Icon name="lock" />
      ) : (
        <Icon name={getFileIcon(file.filename)} />
      )}
      {getFileName(file.filename)}
    </Table.Cell>
    <Table.Cell className="filelist-size">{formatBytes(file.size)}</Table.Cell>
    <Table.Cell className="filelist-attributes">
      {formatAttributes(file)}
    </Table.Cell>
    <Table.Cell className="filelist-length">
      {formatSeconds(file.length)}
    </Table.Cell>
  </Table.Row>
);

// childFiles is pre-filtered by the caller; no filtering happens here
const DirectoryRow = ({
  childFiles,
  dir,
  directorySuffix,
  disabled,
  indent,
  isExpanded,
  onExpand,
  onSelectionChange,
  sep,
}) => {
  const totalSize = childFiles.reduce((sum, f) => sum + (f.size || 0), 0);
  const totalLength = childFiles.reduce((sum, f) => sum + (f.length || 0), 0);
  const allChildSelected =
    childFiles.length > 0 && childFiles.every((f) => f.selected);
  const dirName = dir.name.split(sep).pop();
  const interactive = onExpand != null;
  const folderIcon = indent
    ? 'folder outline'
    : isExpanded
      ? 'folder open'
      : 'folder';

  return (
    <Table.Row
      onClick={interactive ? () => onExpand(dir.name) : undefined}
      style={interactive ? { cursor: 'pointer' } : undefined}
    >
      <Table.Cell
        className="filelist-selector"
        onClick={interactive ? (e) => e.stopPropagation() : undefined}
      >
        {onSelectionChange != null && (
          <Checkbox
            checked={allChildSelected}
            disabled={disabled}
            fitted
            onChange={(_, data) => onSelectionChange(childFiles, data.checked)}
          />
        )}
      </Table.Cell>
      <Table.Cell
        className="filelist-filename"
        style={indent ? { paddingLeft: '2em' } : undefined}
      >
        <Icon name={folderIcon} />
        {dirName}
        <span className="browse-folderlist-caption">
          {formatDirCaption(dir.totalFileCount, dir.totalDirectoryCount)}
        </span>
        {directorySuffix && (
          <span
            className="browse-folderlist-suffix"
            onClick={interactive ? (e) => e.stopPropagation() : undefined}
            onKeyDown={interactive ? (e) => e.stopPropagation() : undefined}
            role={interactive ? 'presentation' : undefined}
          >
            {directorySuffix(dir)}
          </span>
        )}
      </Table.Cell>
      <Table.Cell className="filelist-size">
        {formatBytes(totalSize)}
      </Table.Cell>
      <Table.Cell className="filelist-attributes">
        {getAttributes(childFiles)}
      </Table.Cell>
      <Table.Cell className="filelist-length">
        {totalLength > 0 ? formatSeconds(totalLength) : ''}
      </Table.Cell>
    </Table.Row>
  );
};

// childFiles contains all files in dir and its subdirs, pre-filtered by the parent
const Directory = ({
  childFiles,
  dir,
  directorySuffix,
  disabled,
  isExpanded,
  onExpand,
  onSelectionChange,
  sep,
}) => (
  <>
    <DirectoryRow
      childFiles={childFiles}
      dir={dir}
      directorySuffix={directorySuffix}
      disabled={disabled}
      isExpanded={isExpanded}
      onExpand={onExpand}
      onSelectionChange={onSelectionChange}
      sep={sep}
    />
    {isExpanded && (
      <>
        {(dir.children || []).map((subChild) => {
          const subPrefix = subChild.name + sep;
          const subChildFiles = childFiles.filter((f) =>
            f.filename.startsWith(subPrefix),
          );
          return (
            <DirectoryRow
              childFiles={subChildFiles}
              dir={subChild}
              directorySuffix={directorySuffix}
              indent
              key={subChild.name}
              sep={sep}
            />
          );
        })}
        {(dir.files || [])
          .slice()
          .sort((a, b) => a.filename.localeCompare(b.filename))
          .map((f) => {
            const fullFilename = `${dir.name}${sep}${f.filename}`;
            return (
              <File
                file={{ ...f, filename: fullFilename }}
                indent
                key={fullFilename}
              />
            );
          })}
      </>
    )}
  </>
);

const FileBrowser = ({
  directorySuffix,
  disabled,
  expandedDirectory,
  files,
  footer,
  locked,
  onClose,
  onExpandedDirectoryChange,
  onSelectionChange,
  rootDirectory,
  separator,
}) => {
  const [folded, setFolded] = useState(false);

  // flatten the list of files that we've been given and turn it into a map
  // keyed on the fully qualified name of the file and with a value of the file object
  const fileMap = useMemo(() => {
    const map = new Map();
    for (const f of files || []) {
      map.set(f.filename, f);
    }

    return map;
  }, [files]);

  const directoryName = rootDirectory?.name || '';
  const sep = separator || '\\';

  const filesInRoot = useMemo(
    () =>
      (rootDirectory?.files || [])
        .map((f) => ({
          ...f,
          fullFilename: `${directoryName}${sep}${f.filename}`,
        }))
        .sort((a, b) => a.filename.localeCompare(b.filename)),
    [directoryName, rootDirectory, sep],
  );

  const childDirs = useMemo(
    () => rootDirectory?.children || [],
    [rootDirectory],
  );

  // Group all files by their direct child directory in a single O(N) pass,
  // so each DirectoryRow receives a pre-filtered slice instead of scanning
  // the full list itself (which would be O(N_dirs × N_files)).
  const filesByDir = useMemo(() => {
    if (!directoryName) return new Map();
    const map = new Map();
    const parentPrefix = directoryName + sep;
    for (const f of files || []) {
      const rest = f.filename.startsWith(parentPrefix)
        ? f.filename.slice(parentPrefix.length)
        : null;
      if (rest === null) continue;
      const sepIdx = rest.indexOf(sep);
      if (sepIdx === -1) continue; // direct file in rootDirectory, not in a subdir
      const childDirName = parentPrefix + rest.slice(0, sepIdx);
      let group = map.get(childDirName);
      if (!group) {
        group = [];
        map.set(childDirName, group);
      }

      group.push(f);
    }

    return map;
  }, [directoryName, files, sep]);

  const allSelected =
    (files?.length ?? 0) > 0 && files.every((f) => f.selected);

  const handleSelectAll = (_, data) => {
    onSelectionChange(data.checked ? (files || []).map((f) => f.filename) : []);
  };

  const handleFileToggle = (file, checked) => {
    const result = [];
    for (const f of files || []) {
      if (f === file ? checked : f.selected) result.push(f.filename);
    }

    onSelectionChange(result);
  };

  const handleDirToggle = (childFiles, checked) => {
    const childSet = new Set(childFiles.map((f) => f.filename));
    const result = [];
    for (const f of files || []) {
      if (childSet.has(f.filename) ? checked : f.selected)
        result.push(f.filename);
    }

    onSelectionChange(result);
  };

  const handleExpandDirectory = (name) => {
    onExpandedDirectoryChange(expandedDirectory === name ? null : name);
  };

  const hasContent = filesInRoot.length > 0 || childDirs.length > 0;

  return (
    <div style={{ opacity: locked ? 0.5 : 1 }}>
      <Header
        className="filelist-header"
        size="small"
      >
        <div>
          <Icon
            link={!locked}
            name={locked ? 'lock' : folded ? 'folder' : 'folder open'}
            onClick={() => !locked && setFolded(!folded)}
            size="large"
          />
          {directoryName}
          {Boolean(onClose) && (
            <Icon
              className="close-button"
              color="red"
              link
              name="close"
              onClick={() => onClose()}
            />
          )}
        </div>
      </Header>
      {!folded && hasContent && (
        <Table>
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell className="filelist-selector">
                <Checkbox
                  checked={allSelected}
                  disabled={disabled}
                  fitted
                  onChange={handleSelectAll}
                />
              </Table.HeaderCell>
              <Table.HeaderCell className="filelist-filename">
                {childDirs.length > 0 ? 'Name' : 'File'}
              </Table.HeaderCell>
              <Table.HeaderCell className="filelist-size">
                Size
              </Table.HeaderCell>
              <Table.HeaderCell className="filelist-attributes">
                Attributes
              </Table.HeaderCell>
              <Table.HeaderCell className="filelist-length">
                Length
              </Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {childDirs.map((child) => (
              <Directory
                childFiles={filesByDir.get(child.name) || []}
                dir={child}
                directorySuffix={directorySuffix}
                disabled={disabled}
                isExpanded={expandedDirectory === child.name}
                key={child.name}
                onExpand={handleExpandDirectory}
                onSelectionChange={handleDirToggle}
                sep={sep}
              />
            ))}
            {filesInRoot.map((f) => {
              const flatFile = fileMap.get(f.fullFilename);
              return (
                <File
                  disabled={disabled}
                  file={flatFile || f}
                  key={f.fullFilename}
                  locked={locked}
                  onSelectionChange={flatFile ? handleFileToggle : undefined}
                />
              );
            })}
          </Table.Body>
          {footer && (
            <Table.Footer fullWidth>
              <Table.Row>
                <Table.HeaderCell colSpan="5">
                  {footer(files || [])}
                </Table.HeaderCell>
              </Table.Row>
            </Table.Footer>
          )}
        </Table>
      )}
    </div>
  );
};

export default React.memo(FileBrowser);
