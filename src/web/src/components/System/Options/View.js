import React from 'react';
import YAML from 'yaml';

import { Icon, Button } from 'semantic-ui-react';
import CodeEditor from '../../Shared/CodeEditor';

const View = ({ options, editAction }) => {
  const { remoteConfiguration } = options;

  const optionsAsYaml = YAML.stringify(options, { simpleKeys: true, sortMapEntries: true })

  return (
    <>
      <div className='view-code-container'>
        <CodeEditor
          value={optionsAsYaml}
          basicSetup={false}
          editable={false}
        />
      </div>
      <div className='footer-buttons'>
        {remoteConfiguration ? 
          <Button primary onClick={() => editAction()}><Icon name='edit'/>Edit</Button> : 
          <Button disabled icon='x'><Icon name='lock'/>Remote Configuration Disabled</Button>}
      </div>
    </>
  );
}

export default View;