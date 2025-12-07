import {
  formatBytes,
  formatBytesAsUnit,
  formatDate,
  getFileName,
} from '../../lib/util';
import React, { Component } from 'react';
import {
  Button,
  Checkbox,
  Header,
  Icon,
  List,
  Popup,
  Progress,
  Table,
} from 'semantic-ui-react';

const getColor = (state) => {
  switch (state) {
    case 'InProgress':
      return { color: 'blue' };
    case 'Completed, Succeeded':
      return { color: 'green' };
    case 'Requested':
    case 'Queued, Locally':
    case 'Queued, Remotely':
    case 'Queued':
      return {};
    case 'Initializing':
      return { color: 'teal' };
    default:
      return { color: 'red' };
  }
};

const isRetryableState = (state) => getColor(state).color === 'red';
const isQueuedState = (state) => state.includes('Queued');

const formatBytesTransferred = ({ size, transferred }) => {
  const [s, sExtension] = formatBytes(size, 1).split(' ');
  const t = formatBytesAsUnit(transferred, sExtension, 1);

  return `${t}/${s} ${sExtension}`;
};

// Regex patterns for field name formatting
const UPPERCASE_PATTERN = /([A-Z])/gu;
const FIRST_CHAR_PATTERN = /^./u;
const DURATION_PATTERN = /^\d{2}:\d{2}:\d{2}/u;

// Convert camelCase to Title Case with spaces
const formatFieldName = (fieldName) => {
  return fieldName
    .replaceAll(UPPERCASE_PATTERN, ' $1')
    .replace(FIRST_CHAR_PATTERN, (string) => string.toUpperCase())
    .trim();
};

// Format value based on field type
const formatValue = (key, value) => {
  if (value === null || value === undefined) {
    return 'N/A';
  }

  const lowerKey = key.toLowerCase();

  // Format datetime fields
  if (
    (lowerKey.includes('at') || lowerKey.includes('time')) &&
    typeof value === 'string' &&
    value.includes(':')
  ) {
    // Check if it's a duration (HH:MM:SS format)
    if (DURATION_PATTERN.test(value)) {
      return value;
    }

    // Otherwise it's a datetime
    return formatDate(value);
  }

  // Format byte-related fields
  if (lowerKey.includes('bytes') || key === 'size') {
    return formatBytes(value);
  }

  // Format speed
  if (lowerKey.includes('speed')) {
    return `${formatBytes(value)}/s`;
  }

  // Format percentage
  if (lowerKey.includes('percent')) {
    if (typeof value === 'number') {
      return `${value.toFixed(2)}%`;
    }

    return String(value);
  }

  return String(value);
};

class TransferList extends Component {
  constructor(props) {
    super(props);

    this.state = {
      isFolded: false,
    };
  }

  handleClick = (file) => {
    const { direction, state } = file;

    if (direction === 'Download') {
      if (isRetryableState(state)) {
        return this.props.onRetryRequested(file);
      }

      if (isQueuedState(state)) {
        return this.props.onPlaceInQueueRequested(file);
      }
    }

    return undefined;
  };

  toggleFolded = () => {
    this.setState((previousState) => ({ isFolded: !previousState.isFolded }));
  };

  renderTransferDetails = (file) => {
    // Fields to display in the popup
    const fields = [
      'id',
      'username',
      'direction',
      'filename',
      'size',
      'startOffset',
      'state',
      'stateDescription',
      'requestedAt',
      'enqueuedAt',
      'startedAt',
      'endedAt',
      'bytesTransferred',
      'averageSpeed',
      'bytesRemaining',
      'elapsedTime',
      'percentComplete',
      'remainingTime',
    ];

    return (
      <Table
        basic="very"
        compact
        size="small"
      >
        <Table.Body>
          {fields.map((field) => {
            const value = file[field];
            return (
              <Table.Row key={field}>
                <Table.Cell style={{ fontWeight: 'bold', paddingRight: '1em' }}>
                  {formatFieldName(field)}
                </Table.Cell>
                <Table.Cell>{formatValue(field, value)}</Table.Cell>
              </Table.Row>
            );
          })}
        </Table.Body>
      </Table>
    );
  };

  render() {
    const { directoryName, files, onSelectionChange } = this.props;
    const { isFolded } = this.state;

    return (
      <div>
        <Header
          className="filelist-header"
          size="small"
        >
          <Icon
            link
            name={isFolded ? 'folder' : 'folder open'}
            onClick={() => this.toggleFolded()}
          />
          {directoryName}
        </Header>
        {isFolded === false ? (
          <List>
            <List.Item>
              <Table>
                <Table.Header>
                  <Table.Row>
                    <Table.HeaderCell className="transferlist-selector">
                      <Checkbox
                        checked={files.filter((f) => !f.selected).length === 0}
                        fitted
                        onChange={(event, data) =>
                          files.map((file) =>
                            onSelectionChange(
                              directoryName,
                              file,
                              data.checked,
                            ),
                          )
                        }
                      />
                    </Table.HeaderCell>
                    <Table.HeaderCell className="transferlist-filename">
                      File
                    </Table.HeaderCell>
                    <Table.HeaderCell className="transferlist-progress">
                      Progress
                    </Table.HeaderCell>
                    <Table.HeaderCell className="transferlist-size">
                      Size
                    </Table.HeaderCell>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {files
                    .sort((a, b) =>
                      getFileName(a.filename).localeCompare(
                        getFileName(b.filename),
                      ),
                    )
                    .map((f) => (
                      <Table.Row key={f.filename}>
                        <Table.Cell className="transferlist-selector">
                          <Checkbox
                            checked={f.selected}
                            fitted
                            onChange={(event, data) =>
                              onSelectionChange(directoryName, f, data.checked)
                            }
                          />
                        </Table.Cell>
                        <Table.Cell className="transferlist-filename">
                          {getFileName(f.filename)}
                        </Table.Cell>
                        <Table.Cell className="transferlist-progress">
                          {f.state === 'InProgress' ? (
                            <Progress
                              color={getColor(f.state).color}
                              percent={Math.round(f.percentComplete)}
                              progress
                              style={{ margin: 0 }}
                            />
                          ) : (
                            <Button
                              fluid
                              size="mini"
                              style={{
                                cursor: f.direction === 'Upload' ? 'unset' : '',
                                margin: 0,
                                padding: 7,
                              }}
                              {...getColor(f.state)}
                              active={f.direction === 'Upload'}
                              onClick={() => this.handleClick(f)}
                            >
                              {f.direction === 'Download' &&
                                isQueuedState(f.state) && (
                                  <Icon name="refresh" />
                                )}
                              {f.direction === 'Download' &&
                                isRetryableState(f.state) && (
                                  <Icon name="redo" />
                                )}
                              {f.state}
                              {f.placeInQueue ? ` (#${f.placeInQueue})` : ''}
                            </Button>
                          )}
                        </Table.Cell>
                        <Table.Cell className="transferlist-size">
                          <div
                            style={{
                              alignItems: 'center',
                              display: 'flex',
                              justifyContent: 'space-between',
                            }}
                          >
                            <span>
                              {formatBytesTransferred({
                                size: f.size,
                                transferred: f.bytesTransferred,
                              })}
                            </span>
                            <Popup
                              content={this.renderTransferDetails(f)}
                              on="click"
                              position="left center"
                              style={{ maxWidth: '600px' }}
                              trigger={
                                <Icon
                                  color="grey"
                                  link
                                  name="info circle"
                                  style={{ margin: 0 }}
                                />
                              }
                              wide="very"
                            />
                          </div>
                        </Table.Cell>
                      </Table.Row>
                    ))}
                </Table.Body>
              </Table>
            </List.Item>
          </List>
        ) : (
          ''
        )}
      </div>
    );
  }
}

export default TransferList;
