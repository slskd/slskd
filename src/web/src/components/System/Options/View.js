import React from 'react';
import { Button, Table } from 'semantic-ui-react';

const View = ({ options, editAction }) => {
  const { remoteConfiguration } = options;

  return (
    <>
      {remoteConfiguration && <div style={{textAlign: 'right'}}>
        <Button primary disabled={!remoteConfiguration} onClick={() => editAction()}>Edit</Button>
      </div>}
      <pre>{JSON.stringify(options, null, 2)}</pre>
    </>
  );
}

export default View;