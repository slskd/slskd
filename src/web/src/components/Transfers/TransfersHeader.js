import React, { useState, useMemo } from 'react';

import {
  Segment,
  Icon,
} from 'semantic-ui-react';

import {
  Div,
  Nbsp,
} from '../Shared';

import ShrinkableDropdownButton from '../Shared/ShrinkableDropdownButton';
import { isStateCancellable, isStateRetryable } from '../../lib/transfers';

const getRetryableFiles = ({ files, retryOption }) => {
  switch (retryOption) {
  case 'Errored':
    return files.filter(file => 
      ['Completed, TimedOut', 
        'Completed, Errored', 
        'Completed, Rejected'].includes(file.state));
  case 'Cancelled':
    return files.filter(file => file.state === 'Completed, Cancelled');
  case 'All':
    return files.filter(file => isStateRetryable(file.state));
  default:
    return [];
  }
}

const getCancellableFiles = ({ files, cancelOption }) => {
  switch (cancelOption) {
  case 'All':
    return files.filter(file => isStateCancellable(file.state));
  case 'Queued':
    return files.filter(file => ['Queued, Locally', 'Queued, Remotely'].includes(file.state));
  case 'In Progress':
    return files.filter(file => file.state === 'InProgress')
  default:
    return [];
  }
}

const getRemovableFiles = ({ files, removeOption }) => {
  switch (removeOption) {
  case 'Succeeded':
    return files.filter(file => file.state === 'Completed, Succeeded');
  case 'Errored':
    return files.filter(file => 
      ['Completed, TimedOut', 
        'Completed, Errored', 
        'Completed, Rejected'].includes(file.state));
  case 'Cancelled':
    return files.filter(file => file.state === 'Completed, Cancelled');
  case 'Completed':
    return files.filter(file => file.state.includes('Completed'));
  default:
    return [];
  }
}

const TransfersHeader = ({ 
  direction, 
  transfers, 
  server = { isConnected: true }, 
  onRetryAll,
  retrying = false,
  onCancelAll,
  cancelling = false, 
  onRemoveAll,
  removing = false,
}) => {
  const [removeOption, setRemoveOption] = useState('Succeeded');
  const [cancelOption, setCancelOption] = useState('All');
  const [retryOption, setRetryOption] = useState('Errored');

  const files = useMemo(() => {
    const files = transfers.reduce((acc, username) => {
      const allUserFiles = username.directories.reduce((acc, directory) => {
        acc = acc.concat(directory.files);
        return acc;
      }, []);
    
      acc = acc.concat(allUserFiles)
      return acc;
    }, []);

    return files.filter(file => file.direction.toLowerCase() === direction);
  }, [transfers, direction]);  

  const empty = files.length === 0;
  const working = retrying || cancelling || removing;

  return (
    <Segment className='transfers-header-segment' raised>
      <div className="transfers-segment-icon"><Icon name={direction} size="big"/></div>
      <Div hidden={empty} className="transfers-header-buttons">
        <ShrinkableDropdownButton
          hidden={direction === 'upload'}
          color='green'
          icon='redo'
          mediaQuery='(max-width: 715px)'
          onClick={() => onRetryAll(getRetryableFiles({ files, retryOption }))}
          disabled={working || empty || !server.isConnected}
          loading={retrying}
          options={[
            { key: 'errored', text: 'Errored', value: 'Errored' },
            { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
            { key: 'all', text: 'All', value: 'All' },
          ]}
          onChange={(_, data) => setRetryOption(data.value)}
        >
          {`Retry ${retryOption === 'All' ? retryOption : `All ${retryOption}`}`}
        </ShrinkableDropdownButton>
        <Nbsp/>
        <ShrinkableDropdownButton
          color='red'
          icon='x'
          mediaQuery='(max-width: 715px)'
          onClick={() => onCancelAll(getCancellableFiles({ files, cancelOption }))}
          disabled={working || empty}
          loading={cancelling}
          options={[
            { key: 'all', text: 'All', value: 'All' },
            { key: 'queued', text: 'Queued', value: 'Queued' },
            { key: 'inProgress', text: 'In Progress', value: 'In Progress' },
          ]}
          onChange={(_, data) => setCancelOption(data.value)}
        >
          {`Cancel ${cancelOption === 'All' ? cancelOption : `All ${cancelOption}`}`}
        </ShrinkableDropdownButton>
        <Nbsp/>
        <ShrinkableDropdownButton
          icon='trash alternate'
          mediaQuery='(max-width: 715px)'
          onClick={() => onRemoveAll(getRemovableFiles({ files, removeOption }))}
          disabled={working || empty}
          loading={removing}
          options={[
            { key: 'succeeded', text: 'Succeeded', value: 'Succeeded' },
            { key: 'errored', text: 'Errored', value: 'Errored' },
            { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
            { key: 'completed', text: 'Completed', value: 'Completed' },
          ]}
          onChange={(_, data) => setRemoveOption(data.value)}
        >
          {`Remove All ${removeOption}`}
        </ShrinkableDropdownButton>
      </Div>
    </Segment>
  )
};

export default TransfersHeader;