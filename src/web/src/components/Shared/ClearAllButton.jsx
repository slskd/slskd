import React, { useState } from 'react';
import { Button, Confirm } from 'semantic-ui-react';

// a "Clear all" button that asks first
const ClearAllButton = ({ confirm, disabled, onConfirm }) => {
  const [open, setOpen] = useState(false);
  const [working, setWorking] = useState(false);

  return (
    <>
      <Button
        className="clear-all-button"
        content="Clear all"
        disabled={disabled || working}
        icon="trash alternate"
        loading={working}
        onClick={() => setOpen(true)}
        size="small"
        type="button"
      />
      <Confirm
        cancelButton="Cancel"
        confirmButton={<Button negative>Clear all</Button>}
        content={confirm}
        onCancel={() => setOpen(false)}
        onConfirm={async () => {
          setOpen(false);
          setWorking(true);

          try {
            await onConfirm();
          } finally {
            setWorking(false);
          }
        }}
        open={open}
        size="tiny"
      />
    </>
  );
};

export default ClearAllButton;
