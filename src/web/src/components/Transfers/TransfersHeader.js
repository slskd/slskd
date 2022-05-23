import React, { useState } from 'react';

import {
  Segment,
  Icon,
} from 'semantic-ui-react';

import {
  Div,
  Nbsp,
} from '../Shared';

import ShrinkableDropdownButton from '../Shared/ShrinkableDropdownButton';
import { reduceTransfersToFiles } from '../../lib/transfers';

const TransfersHeader = ({ direction, transfers, server = { isConnected: true }, onRetryAll, onCancelAll, onRemoveAll }) => {
  const [removeOption, setRemoveOption] = useState('Succeeded');
  const [cancelOption, setCancelOption] = useState('All');
  const [retryOption, setRetryOption] = useState('Errored');

  // reduce the given transfers map to an array of files, filtered by direction
  const files = reduceTransfersToFiles(transfers)
    .filter(file => file.direction.toLowerCase() === direction);

  const empty = files.length === 0;

  return (
    <Segment className='transfers-header-segment' raised>
      <div className="transfers-segment-icon"><Icon name={direction} size="big"/></div>
      <Div hidden={empty} className="transfers-header-buttons">
        <ShrinkableDropdownButton
          hidden={direction === 'upload'}
          color='green'
          icon='redo'
          mediaQuery='(max-width: 715px)'
          onClick={onRetryAll}
          disabled={empty || !server.isConnected}
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
          onClick={onCancelAll}
          disabled={empty}
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
          onClick={onRemoveAll}
          disabled={empty}
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