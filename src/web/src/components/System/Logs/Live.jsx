import '../System.css';
import { createLogsHubConnection } from '../../../lib/hubFactory';
import { LoaderSegment } from '../../Shared';
import LogTable from './LogTable';
import React, { Component } from 'react';

const initialState = {
  connected: false,
  logs: [],
};

const maxLogs = 500;

class Live extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    const logsHub = createLogsHubConnection();

    logsHub.on('buffer', (buffer) => {
      this.setState({
        connected: true,
        logs: buffer.reverse().slice(0, maxLogs).map(this.withId),
      });
    });

    logsHub.on('log', (log) => {
      const record = this.withId(log);

      this.setState((previousState) => ({
        connected: true,
        logs: [record].concat(previousState.logs).slice(0, maxLogs),
      }));
    });

    logsHub.onreconnecting(() => this.setState({ connected: false }));
    logsHub.onclose(() => this.setState({ connected: false }));
    logsHub.onreconnected(() => this.setState({ connected: true }));

    logsHub.start();
  }

  // log records carry nothing unique; timestamps are second precision and the
  // same line can repeat within a second.  tag each record as it arrives so the
  // table has a stable, unique key.  never reset, so records from a replayed
  // buffer can't collide with those already rendered.
  nextId = 0;

  withId = (log) => ({ ...log, id: this.nextId++ });

  render() {
    const { connected, logs } = this.state;

    return (
      <div className="logs">
        {!connected && <LoaderSegment />}
        {connected && <LogTable logs={logs} />}
      </div>
    );
  }
}

export default Live;
