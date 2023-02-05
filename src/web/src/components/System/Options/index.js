import React, { useState, useEffect } from 'react';
import YAML from 'yaml';

import DebugModal from './DebugModal';
import EditModal from './EditModal';

import { Divider } from 'semantic-ui-react';
import {
  CodeEditor,
  LoaderSegment,
  ShrinkableButton,
  Switch,
} from '../../Shared';

const Index = ({ options, theme }) => {
  const [debugModal, setDebugModal] = useState(false);
  const [editModal, setEditModal] = useState(false);
  const [contents, setContents] = useState();

  useEffect(() => {
    setTimeout(() => {
      setContents(YAML.stringify(options, { simpleKeys: true, sortMapEntries: false }));
    }, 250);
  }, [options]);
  
  const { remoteConfiguration, debug } = options;

  const DebugButton = ({ ...props }) => {
    if (!remoteConfiguration || !debug) return <></>;
    
    return <ShrinkableButton
      icon='bug'
      mediaQuery='(max-width: 516px)'
      onClick={() => setDebugModal(true)}
      {...props}
    >
      Debug View
    </ShrinkableButton>;
  };

  const EditButton = ({ ...props }) => {
    if (!remoteConfiguration) {
      return <ShrinkableButton 
        disabled 
        icon='lock' 
        mediaQuery='(max-width: 516px)'
      >Remote Configuration Disabled</ShrinkableButton>;
    }

    return <ShrinkableButton 
      primary
      icon='edit'
      mediaQuery='(max-width: 516px)'
      onClick={() => setEditModal(true)}
      {...props}
    >Edit</ShrinkableButton>; 
  };

  return (
    <>
      <div className='header-buttons'>
        <DebugButton disabled={!contents}/>
        <EditButton disabled={!contents}/>
      </div>
      <Divider/>
      <Switch
        loading={!contents && <LoaderSegment/>}
      >
        <CodeEditor
          value={contents}
          basicSetup={false}
          editable={false}
          theme={theme}
        />
      </Switch>
      <DebugModal
        open={debugModal}
        theme={theme}
        onClose={() => setDebugModal(false)}
      />
      <EditModal
        open={editModal}
        theme={theme}
        onClose={() => setEditModal(false)}
      />
    </>
  );
};

export default Index;