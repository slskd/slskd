import React, { useState, useEffect } from 'react';
import YAML from 'yaml';

import { restart, shutdown, getVersion } from '../../../lib/application';

import { Modal, Header, Divider } from 'semantic-ui-react';

import {
  CodeEditor,
  LoaderSegment,
  ShrinkableButton,
  Switch,
} from '../../Shared';

const Info = ({ state }) => {
  const [contents, setContents] = useState();

  useEffect(() => {
    setTimeout(() => {
      setContents(YAML.stringify(state, { simpleKeys: true, sortMapEntries: true }));
    }, 250);
  }, [state]);

  return (
    <>
      <div className='header-buttons'>
        <div style={{float: 'left'}}>
          <ShrinkableButton
            icon='refresh'
            mediaQuery='(max-width: 516px)'
            primary
            disabled={!contents}
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
              disabled={!contents}
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
              disabled={!contents}
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
      <Divider/>
      <Switch
        loading={!contents && <LoaderSegment/>}
      >
        <CodeEditor
          value={contents}
          basicSetup={false}
          editable={false}
        />
      </Switch>
    </>
  );
};

export default Info;