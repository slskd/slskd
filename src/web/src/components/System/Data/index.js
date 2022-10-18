import React, { useState } from 'react';
import { Button, Divider, Header, Icon } from 'semantic-ui-react';
import { toast } from 'react-toastify';

import { clearCompleted } from '../../../lib/transfers';

const Data = () => {
  const [up, setUp] = useState(false);
  const [down, setDown] = useState(false);

  const clear = async ({ direction, setState }) => {
    setState(true);
    await clearCompleted({ direction });
    setState(false);
    toast.success(`Completed ${direction}s cleared!`);
  };

  return (
    <div>
      <Header as='h3' className='transfer-header'>Transfer Data</Header>
      <Divider/>
      <p>
        The Uploads and Downloads pages can become unresponsive if too many transfers are displayed. If you're having trouble with either page, try using the buttons below to remove completed transfers.
      </p>
      <Button
        primary
        loading={up}
        onClick={() => clear({ direction: 'upload', setState: setUp })}
      >
        <Icon name='trash alternate'/>
        Clear All Completed Uploads
      </Button>
      <Button
        primary
        loading={down}
        onClick={() => clear({ direction: 'download', setState: setDown })}
      >
        <Icon name='trash alternate'/>
        Clear All Completed Downloads
      </Button>
    </div>
  );
};

export default Data;