import React, { useState } from 'react';

import {
  Segment,
  Button,
  Dropdown,
  Icon,
} from 'semantic-ui-react';

import ShrinkableButton from '../Shared/ShrinkableButton';

const TransfersHeader = ({ direction, count, onRetryAll, onCancelAll, onRemoveAll }) => {
  const [removeOption, setRemoveOption] = useState('Succeeded');
  const [cancelOption, setCancelOption] = useState('All');
  const [retryOption, setRetryOption] = useState('Errored');

  const empty = count === 0;

  return (
    <>
      <Segment className='transfers-header-segment' raised>
        <div className="transfers-segment-icon"><Icon name={direction} size="big"/></div>
        <div className="transfers-header-buttons">
          {direction === 'download' && <Button.Group color='green'>
            <ShrinkableButton
              icon='redo'
              onClick={onRetryAll}
              mediaQuery='(max-width: 766px)'
              disabled={empty}
            >{`Retry ${retryOption === 'All' ? retryOption : `All ${retryOption}`}`}</ShrinkableButton>
            <Dropdown
              disabled={empty}
              className='button icon'
              options={[
                { key: 'errored', text: 'Errored', value: 'Errored' },
                { key: 'cancelled', text: 'Cancelled', value: 'Cancelled' },
                { key: 'all', text: 'All', value: 'All' },
              ]}
              onChange={(_, data) => setRetryOption(data.value)}
              trigger={<></>}
            />
          </Button.Group>}
          {direction === 'download' && ' '}
          <Button.Group color='red'>
            <ShrinkableButton
              icon='x'
              onClick={onCancelAll}
              mediaQuery='(max-width: 766px)'
              disabled={empty}
            >{`Cancel ${cancelOption === 'All' ? cancelOption : `All ${cancelOption}`}`}</ShrinkableButton>
            <Dropdown
              className='button icon'
              disabled={empty}
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
            <ShrinkableButton
              icon='trash alternate'
              mediaQuery='(max-width: 766px)'
              disabled={empty}
              onClick={onRemoveAll}
            >{`Remove All ${removeOption}`}</ShrinkableButton>
            <Dropdown
              className='button icon'
              disabled={empty}
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
        </div>
      </Segment>
    </>
  )
};

export default TransfersHeader;