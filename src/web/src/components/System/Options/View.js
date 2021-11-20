import React from 'react';
import yaml from 'yaml';

import { Icon, Button } from 'semantic-ui-react';
import CodeEditor from './CodeEditor';

const View = ({ options, editAction }) => {
  const { remoteConfiguration } = options;

  const doc = new yaml.Document();
  doc.contents = options;

  const optionsAsYaml = doc.toString();

  return (
    <>
      <div className='code-container'>
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