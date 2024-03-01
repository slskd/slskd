import React, { Component } from 'react';
import { Table, Loader, Dimmer, Button, Grid } from 'semantic-ui-react';

import { createLogsHubConnection } from '../../../lib/hubFactory';
import { LoaderSegment } from '../../Shared';

import '../System.css';


const defaultLimit = 25;

const initialState = {
  logs: [],
  connected: false,
  limit: defaultLimit,
};

const levels = {
  'Debug': 'DBG',
  'Warning': 'WRN',
  'Error': 'ERR',
  'Information': 'INF',
};


class Logs extends Component {
  state = initialState;

  componentDidMount = () => {
    const logsHub = createLogsHubConnection();

    logsHub.on('buffer', (buffer) => {
      this.setState({ connected: true, logs: buffer.reverse() });
    })

    logsHub.on('log', (log) => {
      this.setState({ connected: true, logs: [log].concat(this.state.logs) });
    })

    logsHub.onreconnecting(() => this.setState({ connected: false }));
    logsHub.onclose(() => this.setState({ connected: false }));
    logsHub.onreconnected(() => this.setState({ connected: true }));

    logsHub.start();
  };

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`; // eslint-disable-line max-len
  };


  formatLogMessage = (log) => {
    return [
      this.formatTimestamp(log.timestamp),
      levels[log.level] || log.level,
      log.message
    ]
  }

  download = () => {
    const messages = this.state.logs.map((log, _) => this.formatLogMessage(log).join('\t'));
    const data = new Blob([messages.slice().reverse().join('\n')], { type: 'text/plain' });
    const url = window.URL.createObjectURL(data);
    const tempLink = document.createElement('a');
    tempLink.href = url;
    tempLink.setAttribute('download', `slskd_${new dayjs().format()}.log`);
    tempLink.click();
  }

  render = () => {
    const { connected, limit, logs } = this.state;
    const logsLength = logs.length
    const logsVisible = logs.slice(0, limit);

    return (
      <div className='logs'>
        <div className='header-buttons'>
          <Button
            color='green'
            content='Download'
            icon='download'
            onClick={() => this.download()}
            disabled={!connected}
          />
        </div>
        {!connected && <Dimmer active inverted><Loader inverted /></Dimmer>}
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
              {logsVisible.map((log, index) => {
                const [timestamp, level, message] = this.formatLogMessage(log);
                return <Table.Row key={index} disabled={level === 'DBG'} warning={level === 'WRN'} negative={level === 'ERR'}>
                  <Table.Cell>{timestamp}</Table.Cell>
                  <Table.Cell>{level}</Table.Cell>
                  <Table.Cell className='logs-table-message'>{message}</Table.Cell>
                </Table.Row>
              })}
            </Table.Body>
          </Table>
        }
        {connected && logsLength > limit &&
          <div className='footer-buttons' style={{ 'text-align': 'center' }}>
            <Button
              className='logs-button'
              color='green'
              content={`Show another ${defaultLimit} lines`}
              onClick={() => this.setState({ limit: limit + 10 })}
            />
          </div>
        }
      </div>
    );
  };
}

export default Logs;