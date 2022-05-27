import React, { Component } from 'react';
import { Table, Loader, Dimmer } from 'semantic-ui-react';

import { createLogsHubConnection } from '../../../lib/hubFactory';

import '../System.css';

const initialState = {
  logs: [],
  connected: false,
};

const levels = {
  'Debug': 'DBG',
  'Warning': 'WRN',
  'Error': 'ERR',
  'Information': 'INF',
};

const maxLogs = 500;

class Logs extends Component {
  state = initialState;

  componentDidMount = () => {
    const logsHub = createLogsHubConnection();

    logsHub.on('buffer', (buffer) => {
      this.setState({ connected: true, logs: buffer.reverse().slice(0, maxLogs) });
    });

    logsHub.on('log', (log) => {
      this.setState({ connected: true, logs: [log].concat(this.state.logs).slice(0, maxLogs) });
    });

    logsHub.onreconnecting(() => this.setState({ connected: false }));
    logsHub.onclose(() => this.setState({ connected: false }));
    logsHub.onreconnected(() => this.setState({ connected: true }));

    logsHub.start();
  };

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`; // eslint-disable-line max-len
  };

  render = () => {
    const { connected, logs } = this.state;

    return (
      <div className='logs'>
        {!connected && <Dimmer active inverted><Loader inverted/></Dimmer>}
        {connected &&
          <Table compact='very' className='logs-table'>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Timestamp</Table.HeaderCell>
                <Table.HeaderCell>Level</Table.HeaderCell>
                <Table.HeaderCell>Message</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body className='logs-table-body'>
              {logs.map((log, index) =>
                <Table.Row
                  key={index}
                  disabled={log.level === 'Debug'}
                  warning={log.level === 'Warning'}
                  negative={log.level === 'Error'}
                >
                  <Table.Cell>{this.formatTimestamp(log.timestamp)}</Table.Cell>
                  <Table.Cell>{levels[log.level] || log.level}</Table.Cell>
                  <Table.Cell className='logs-table-message'>{log.message}</Table.Cell>
                </Table.Row>)}
            </Table.Body>
          </Table>
        }
      </div>
    );
  };
}

export default Logs;