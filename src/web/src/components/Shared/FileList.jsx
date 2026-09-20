import {
  formatAttributes,
  formatBytes,
  formatSeconds,
  getFileName,
} from '../../lib/util';
import React, { useCallback, useMemo, useState } from 'react';
import { Checkbox, Header, Icon, List, Table } from 'semantic-ui-react';

const FileList = ({
  directoryName,
  disabled,
  files,
  footer,
  locked,
  onClose,
  onSelectionChange,
}) => {
  const [folded, setFolded] = useState(false);
  const [lastClickedIndex, setLastClickedIndex] = useState(null);

  const sortedFiles = useMemo(() => {
    if (!files) {
      return [];
    }

    return [...files].sort((a, b) => (a.filename > b.filename ? 1 : -1));
  }, [files]);

  const handleFileCheck = useCallback(
    (event, data, clickedIndex) => {
      const { checked } = data;
      const { nativeEvent } = event;

      if (nativeEvent.shiftKey && lastClickedIndex !== null) {
        const start = Math.min(lastClickedIndex, clickedIndex);
        const end = Math.max(lastClickedIndex, clickedIndex);

        const filesToUpdate = sortedFiles.slice(start, end + 1);

        for (const file of filesToUpdate) {
          onSelectionChange(file, checked);
        }
      } else {
        const file = sortedFiles[clickedIndex];
        onSelectionChange(file, checked);
      }

      if (!nativeEvent.shiftKey) {
        setLastClickedIndex(clickedIndex);
      }
    },
    [lastClickedIndex, onSelectionChange, sortedFiles],
  );

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
      {!folded && sortedFiles && sortedFiles.length > 0 && (
        <List>
          <List.Item>
            <Table>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell className="filelist-selector">
                    <Checkbox
                      checked={
                        sortedFiles.filter((f) => !f.selected).length === 0
                      }
                      disabled={disabled}
                      fitted
                      onChange={(event, data) => {
                        sortedFiles.map((f) =>
                          onSelectionChange(f, data.checked),
                        );
                        setLastClickedIndex(null);
                      }}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell className="filelist-filename">
                    File
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
                {sortedFiles.map((f, index) => (
                  <Table.Row key={f.filename}>
                    <Table.Cell className="filelist-selector">
                      <Checkbox
                        checked={f.selected}
                        disabled={disabled}
                        fitted
                        onChange={(event, data) =>
                          handleFileCheck(event, data, index)
                        }
                      />
                    </Table.Cell>
                    <Table.Cell className="filelist-filename">
                      {locked ? <Icon name="lock" /> : ''}
                      {getFileName(f.filename)}
                    </Table.Cell>
                    <Table.Cell className="filelist-size">
                      {formatBytes(f.size)}
                    </Table.Cell>
                    <Table.Cell className="filelist-attributes">
                      {formatAttributes(f)}
                    </Table.Cell>
                    <Table.Cell className="filelist-length">
                      {formatSeconds(f.length)}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
              {footer && (
                <Table.Footer fullWidth>
                  <Table.Row>
                    <Table.HeaderCell colSpan="5">{footer}</Table.HeaderCell>
                  </Table.Row>
                </Table.Footer>
              )}
            </Table>
          </List.Item>
        </List>
      )}
    </div>
  );
};

export default FileList;
