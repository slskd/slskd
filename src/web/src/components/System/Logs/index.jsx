import '../System.css';
import { createLogsHubConnection } from '../../../lib/hubFactory';
import { LoaderSegment } from '../../Shared';
import React, { Component } from 'react';
import { Table } from 'semantic-ui-react';

const initialState = {
  connected: false,
  logs: [],
};

const levels = {
  Debug: 'DBG',
  Error: 'ERR',
  Information: 'INF',
  Warning: 'WRN',
};

const maxLogs = 500;

class Logs extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    const logsHub = createLogsHubConnection();

    logsHub.on('buffer', (buffer) => {
      this.setState({
        connected: true,
        logs: buffer.reverse().slice(0, maxLogs),
      });
    });

    logsHub.on('log', (log) => {
      this.setState((previousState) => ({
        connected: true,
        logs: [log].concat(previousState.logs).slice(0, maxLogs),
      }));
    });

    logsHub.onreconnecting(() => this.setState({ connected: false }));
    logsHub.onclose(() => this.setState({ connected: false }));
    logsHub.onreconnected(() => this.setState({ connected: true }));

    logsHub.start();
  }

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`; // eslint-disable-line max-len
  };

  render() {
    const { connected, logs } = this.state;

    return (
      <div className="logs">
        {!connected && <LoaderSegment />}
        {connected && (
          <Table
            className="logs-table"
            compact="very"
          >
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Timestamp</Table.HeaderCell>
                <Table.HeaderCell>Level</Table.HeaderCell>
                <Table.HeaderCell>Message</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body className="logs-table-body">
              {logs.map((log) => (
                <Table.Row
                  disabled={log.level === 'Debug'}
                  key={log.timestamp}
                  negative={log.level === 'Error'}
                  warning={log.level === 'Warning'}
                >
                  <Table.Cell>{this.formatTimestamp(log.timestamp)}</Table.Cell>
                  <Table.Cell>{levels[log.level] || log.level}</Table.Cell>
                  <Table.Cell className="logs-table-message">
                    {log.message}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
      </div>
    );
  }
}

export default Logs;
