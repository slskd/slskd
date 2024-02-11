import {
  CodeEditor,
  LoaderSegment,
  ShrinkableButton,
  Switch,
} from '../../Shared';
import DebugModal from './DebugModal';
import EditModal from './EditModal';
import React, { useEffect, useState } from 'react';
import { Divider } from 'semantic-ui-react';
import YAML from 'yaml';

const Index = ({ options, theme }) => {
  const [debugModal, setDebugModal] = useState(false);
  const [editModal, setEditModal] = useState(false);
  const [contents, setContents] = useState();

  useEffect(() => {
    setTimeout(() => {
      setContents(
        YAML.stringify(options, { simpleKeys: true, sortMapEntries: false }),
      );
    }, 250);
  }, [options]);

  const { debug, remoteConfiguration } = options;

  const DebugButton = ({ ...props }) => {
    if (!remoteConfiguration || !debug) return <></>;

    return (
      <ShrinkableButton
        icon="bug"
        mediaQuery="(max-width: 516px)"
        onClick={() => setDebugModal(true)}
        {...props}
      >
        Debug View
      </ShrinkableButton>
    );
  };

  const EditButton = ({ ...props }) => {
    if (!remoteConfiguration) {
      return (
        <ShrinkableButton
          disabled
          icon="lock"
          mediaQuery="(max-width: 516px)"
        >
          Remote Configuration Disabled
        </ShrinkableButton>
      );
    }

    return (
      <ShrinkableButton
        icon="edit"
        mediaQuery="(max-width: 516px)"
        onClick={() => setEditModal(true)}
        primary
        {...props}
      >
        Edit
      </ShrinkableButton>
    );
  };

  return (
    <>
      <div className="header-buttons">
        <DebugButton disabled={!contents} />
        <EditButton disabled={!contents} />
      </div>
      <Divider />
      <Switch loading={!contents && <LoaderSegment />}>
        <CodeEditor
          basicSetup={false}
          editable={false}
          theme={theme}
          value={contents}
        />
      </Switch>
      <DebugModal
        onClose={() => setDebugModal(false)}
        open={debugModal}
        theme={theme}
      />
      <EditModal
        onClose={() => setEditModal(false)}
        open={editModal}
        theme={theme}
      />
    </>
  );
};

export default Index;
