import {
  formatBytes,
  formatBytesAsUnit,
  formatRemainingTime,
  formatSpeed,
  getFileName,
} from '../../lib/util';
import React, { Component } from 'react';
import {
  Button,
  Checkbox,
  Header,
  Icon,
  List,
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
                    <Table.HeaderCell className="transferlist-speed">
                      Speed
                    </Table.HeaderCell>
                    <Table.HeaderCell className="transferlist-eta">
                      ETA
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
                          {formatBytesTransferred({
                            size: f.size,
                            transferred: f.bytesTransferred,
                          })}
                        </Table.Cell>
                        <Table.Cell className="transferlist-speed">
                          {f.state === 'InProgress'
                            ? formatSpeed(f.averageSpeed)
                            : ''}
                        </Table.Cell>
                        <Table.Cell className="transferlist-eta">
                          {f.state === 'InProgress'
                            ? formatRemainingTime(f.remainingTime)
                            : ''}
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
