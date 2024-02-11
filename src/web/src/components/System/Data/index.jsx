import { clearCompleted } from '../../../lib/transfers';
import React, { useState } from 'react';
import { toast } from 'react-toastify';
import { Button, Divider, Header, Icon } from 'semantic-ui-react';

const clear = async ({ direction, setState }) => {
  setState(true);
  await clearCompleted({ direction });
  setState(false);
  toast.success(`Completed ${direction}s cleared!`);
};

const Data = () => {
  const [up, setUp] = useState(false);
  const [down, setDown] = useState(false);

  return (
    <div>
      <Header
        as="h3"
        className="transfer-header"
      >
        Transfer Data
      </Header>
      <Divider />
      <p>
        <span>
          The Uploads and Downloads pages can become unresponsive if too many
          transfers are displayed. If you're having trouble with either page,
          try using the buttons below to remove completed transfers.
        </span>
      </p>
      <Button
        loading={up}
        onClick={() => clear({ direction: 'upload', setState: setUp })}
        primary
      >
        <Icon name="trash alternate" />
        Clear All Completed Uploads
      </Button>
      <Button
        loading={down}
        onClick={() => clear({ direction: 'download', setState: setDown })}
        primary
      >
        <Icon name="trash alternate" />
        Clear All Completed Downloads
      </Button>
    </div>
  );
};

export default Data;
