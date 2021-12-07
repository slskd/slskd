import React from 'react';
import YAML from 'yaml';

import { restart, shutdown, getVersion } from '../../lib/application';

import { Button, Icon, Modal, Header } from 'semantic-ui-react';
import CodeEditor from '../Shared/CodeEditor';

const Info = ({ state }) => {
  const stateAsYaml = YAML.stringify(state, { simpleKeys: true, sortMapEntries: true })

  return (
    <>
      <div className='view-code-container'>
        <CodeEditor
          value={stateAsYaml}
          basicSetup={false}
          editable={false}
        />
      </div>
      <div className='footer-buttons'>
        <div style={{float: 'left'}}>
          <Button onClick={() => getVersion({ forceCheck: true })}><Icon name='search'/>Check for Updates</Button>
        </div>
        <Modal
          trigger={
            <Button><Icon name='shutdown'/>Shut Down</Button>
          }
          centered
          size='mini'
          header={<Header icon='redo' content='Confirm Shutdown' />}
          content="Are you sure you want shut the application down?  You'll need to manually start it again."
          actions={['Cancel', { key: 'done', content: 'Shut Down', negative: true, onClick: shutdown }]}
        />
        <Modal
          trigger={
            <Button><Icon name='redo'/>Restart</Button>
          }
          centered
          size='mini'
          header={<Header icon='redo' content='Confirm Restart' />}
          content='Are you sure you want restart the application?'
          actions={['Cancel', { key: 'done', content: 'Restart', negative: true, onClick: restart }]}
        />
      </div>
    </>
  );
}

export default Info;