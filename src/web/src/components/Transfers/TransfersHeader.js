import React, { useState } from 'react';

import {
  Segment,
  Button,
  Dropdown,
} from 'semantic-ui-react';

const TransfersHeader = ({ onRetryAll, onCancelAll, onRemoveAll }) => {
  const [removeOption, setRemoveOption] = useState('Succeeded');
  const [cancelOption, setCancelOption] = useState('All');
  const [retryOption, setRetryOption] = useState('Errored');

  return (
    <>
      <Segment className='transfers-header-segment' raised>
        <Button.Group color='green'>
          <Button
            icon='redo'
            onClick={onRetryAll}
            content={`Retry ${retryOption === 'All' ? retryOption : `All ${retryOption}`}`}
          />
          <Dropdown
            className='button icon'
            options={[
              { key: 'errored', text: 'Errored', value: 'Errored' },
              { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
              { key: 'all', text: 'All', value: 'All' },
            ]}
            onChange={(_, data) => setRetryOption(data.value)}
            trigger={<></>}
          />
        </Button.Group>
        {' '}
        <Button.Group color='red'>
          <Button
            icon='x'
            onClick={onCancelAll}
            content={`Cancel ${cancelOption === 'All' ? cancelOption : `All ${cancelOption}`}`}
          />
          <Dropdown
            className='button icon'
            options={[
              { key: 'all', text: 'All', value: 'All' },
              { key: 'queued', text: 'Queued', value: 'Queued' },
              { key: 'inProgress', text: 'In Progress', value: 'In Progress' },
            ]}
            onChange={(_, data) => setCancelOption(data.value)}
            trigger={<></>}
          />
        </Button.Group>
        {' '}
        <Button.Group>
          <Button
            icon='trash alternate'
            onClick={onRemoveAll}
            content={`Remove All ${removeOption}`}
          />
          <Dropdown
            className='button icon'
            options={[
              { key: 'succeeded', text: 'Succeeded', value: 'Succeeded' },
              { key: 'errored', text: 'Errored', value: 'Errored' },
              { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
              { key: 'completed', text: 'Completed', value: 'Completed' },
            ]}
            onChange={(_, data) => setRemoveOption(data.value)}
            trigger={<></>}
          />
        </Button.Group>
      </Segment>
    </>
  )
};

export default TransfersHeader;