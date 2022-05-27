import React, { useState } from 'react';
import YAML from 'yaml';

import DebugModal from './DebugModal';
import EditModal from './EditModal';

import { Divider } from 'semantic-ui-react';
import {
  CodeEditor,
  ShrinkableButton,
} from '../../Shared';

const Index = ({ options }) => {
  const [debugModal, setDebugModal] = useState(false);
  const [editModal, setEditModal] = useState(false);
  
  const { remoteConfiguration, debug } = options;

  const optionsAsYaml = YAML.stringify(options, { simpleKeys: true, sortMapEntries: true });

  const DebugButton = () => {
    if (!debug) return <></>;
    
    return <ShrinkableButton
      icon='bug'
      mediaQuery='(max-width: 516px)'
      onClick={() => setDebugModal(true)}
    >
      Debug View
    </ShrinkableButton>;
  };

  const EditButton = () => {
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
    >Edit</ShrinkableButton>; 
  };

  return (
    <>
      <div className='header-buttons'>
        <DebugButton/>
        <EditButton/>
      </div>
      <Divider/>
      <CodeEditor
        style={{minHeight: 500}}
        value={optionsAsYaml}
        basicSetup={false}
        editable={false}
      />
      <DebugModal
        open={debugModal}
        onClose={() => setDebugModal(false)}
      />
      <EditModal
        open={editModal}
        onClose={() => setEditModal(false)}
      />
    </>
  );
};

export default Index;