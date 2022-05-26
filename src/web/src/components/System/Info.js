import React from 'react';
import YAML from 'yaml';

import { restart, shutdown, getVersion } from '../../lib/application';

import { Modal, Header } from 'semantic-ui-react';
import CodeEditor from '../Shared/CodeEditor';
import ShrinkableButton from '../Shared/ShrinkableButton';

const Info = ({ state }) => {
  const stateAsYaml = YAML.stringify(state, { simpleKeys: true, sortMapEntries: true });

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
          <ShrinkableButton
            icon='refresh'
            mediaQuery='(max-width: 516px)'
            primary
            onClick={() => getVersion({ forceCheck: true })}
          >
            Check for Updates
          </ShrinkableButton>
        </div>
        <Modal
          trigger={
            <ShrinkableButton
              icon='shutdown'
              mediaQuery='(max-width: 516px)'
              negative
            >
              Shut Down
            </ShrinkableButton>
          }
          centered
          size='mini'
          header={<Header icon='redo' content='Confirm Shutdown' />}
          content="Are you sure you want to shut the application down?  You'll need to manually start it again."
          actions={['Cancel', { key: 'done', content: 'Shut Down', negative: true, onClick: shutdown }]}
        />
        <Modal
          trigger={
            <ShrinkableButton
              icon='redo'
              mediaQuery='(max-width: 516px)'
              negative
            >
              Restart
            </ShrinkableButton>
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
};

export default Info;