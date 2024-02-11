import { isStateCancellable, isStateRetryable } from '../../lib/transfers';
import { Div, Nbsp } from '../Shared';
import ShrinkableDropdownButton from '../Shared/ShrinkableDropdownButton';
import React, { useMemo, useState } from 'react';
import { Icon, Segment } from 'semantic-ui-react';

const getRetryableFiles = ({ files, retryOption }) => {
  switch (retryOption) {
    case 'Errored':
      return files.filter((file) =>
        [
          'Completed, TimedOut',
          'Completed, Errored',
          'Completed, Rejected',
        ].includes(file.state),
      );
    case 'Cancelled':
      return files.filter((file) => file.state === 'Completed, Cancelled');
    case 'All':
      return files.filter((file) => isStateRetryable(file.state));
    default:
      return [];
  }
};

const getCancellableFiles = ({ cancelOption, files }) => {
  switch (cancelOption) {
    case 'All':
      return files.filter((file) => isStateCancellable(file.state));
    case 'Queued':
      return files.filter((file) =>
        ['Queued, Locally', 'Queued, Remotely'].includes(file.state),
      );
    case 'In Progress':
      return files.filter((file) => file.state === 'InProgress');
    default:
      return [];
  }
};

const getRemovableFiles = ({ files, removeOption }) => {
  switch (removeOption) {
    case 'Succeeded':
      return files.filter((file) => file.state === 'Completed, Succeeded');
    case 'Errored':
      return files.filter((file) =>
        [
          'Completed, TimedOut',
          'Completed, Errored',
          'Completed, Rejected',
        ].includes(file.state),
      );
    case 'Cancelled':
      return files.filter((file) => file.state === 'Completed, Cancelled');
    case 'Completed':
      return files.filter((file) => file.state.includes('Completed'));
    default:
      return [];
  }
};

const TransfersHeader = ({
  cancelling = false,
  direction,
  onCancelAll,
  onRemoveAll,
  onRetryAll,
  removing = false,
  retrying = false,
  server = { isConnected: true },
  transfers,
}) => {
  const [removeOption, setRemoveOption] = useState('Succeeded');
  const [cancelOption, setCancelOption] = useState('All');
  const [retryOption, setRetryOption] = useState('Errored');

  const files = useMemo(() => {
    const files = transfers.reduce((accumulator, username) => {
      const allUserFiles = username.directories.reduce(
        (accumulator_, directory) => {
          accumulator_ = accumulator_.concat(directory.files);
          return accumulator_;
        },
        [],
      );

      accumulator = accumulator.concat(allUserFiles);
      return accumulator;
    }, []);

    return files.filter((file) => file.direction.toLowerCase() === direction);
  }, [direction, transfers]);

  const empty = files.length === 0;
  const working = retrying || cancelling || removing;

  return (
    <Segment
      className="transfers-header-segment"
      raised
    >
      <div className="transfers-segment-icon">
        <Icon
          name={direction}
          size="big"
        />
      </div>
      <Div
        className="transfers-header-buttons"
        hidden={empty}
      >
        <ShrinkableDropdownButton
          color="green"
          disabled={working || empty || !server.isConnected}
          hidden={direction === 'upload'}
          icon="redo"
          loading={retrying}
          mediaQuery="(max-width: 715px)"
          onChange={(_, data) => setRetryOption(data.value)}
          onClick={() => onRetryAll(getRetryableFiles({ files, retryOption }))}
          options={[
            { key: 'errored', text: 'Errored', value: 'Errored' },
            { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
            { key: 'all', text: 'All', value: 'All' },
          ]}
        >
          {`Retry ${retryOption === 'All' ? retryOption : `All ${retryOption}`}`}
        </ShrinkableDropdownButton>
        <Nbsp />
        <ShrinkableDropdownButton
          color="red"
          disabled={working || empty}
          icon="x"
          loading={cancelling}
          mediaQuery="(max-width: 715px)"
          onChange={(_, data) => setCancelOption(data.value)}
          onClick={() =>
            onCancelAll(getCancellableFiles({ cancelOption, files }))
          }
          options={[
            { key: 'all', text: 'All', value: 'All' },
            { key: 'queued', text: 'Queued', value: 'Queued' },
            { key: 'inProgress', text: 'In Progress', value: 'In Progress' },
          ]}
        >
          {`Cancel ${cancelOption === 'All' ? cancelOption : `All ${cancelOption}`}`}
        </ShrinkableDropdownButton>
        <Nbsp />
        <ShrinkableDropdownButton
          disabled={working || empty}
          icon="trash alternate"
          loading={removing}
          mediaQuery="(max-width: 715px)"
          onChange={(_, data) => setRemoveOption(data.value)}
          onClick={() =>
            onRemoveAll(getRemovableFiles({ files, removeOption }))
          }
          options={[
            { key: 'succeeded', text: 'Succeeded', value: 'Succeeded' },
            { key: 'errored', text: 'Errored', value: 'Errored' },
            { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
            { key: 'completed', text: 'Completed', value: 'Completed' },
          ]}
        >
          {`Remove All ${removeOption}`}
        </ShrinkableDropdownButton>
      </Div>
    </Segment>
  );
};

export default TransfersHeader;
