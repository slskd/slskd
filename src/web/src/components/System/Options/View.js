import React from 'react';
import yaml from 'yaml';

import { Icon, Button } from 'semantic-ui-react';
import CodeEditor from '@uiw/react-textarea-code-editor';

const View = ({ options, editAction }) => {
  const { remoteConfiguration } = options;

  const doc = new yaml.Document();
  doc.contents = options;

  const optionsAsYaml = doc.toString();

  return (
    <>
      <CodeEditor
        value={optionsAsYaml}
        language='yaml'
        disabled={true}
        padding={10}
        style={{
          border: '1px solid #d4d4d5',
          fontSize: '1em',
          fontFamily: 'ui-monospace,SFMono-Regular,SF Mono,Consolas,Liberation Mono,Menlo,monospace',
          overflow: 'auto',
          height: 'calc(100vh - 254px)'
        }}
      />
      <div className='footer-buttons'>
        {remoteConfiguration ? 
          <Button primary onClick={() => editAction()}><Icon name='edit'/>Edit</Button> : 
          <Button disabled icon='x'><Icon name='lock'/>Remote Configuration Disabled</Button>}
      </div>
    </>
  );
}

export default View;