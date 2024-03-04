import { getVersion, restart, shutdown } from '../../../lib/application';
import {
  CodeEditor,
  LoaderSegment,
  ShrinkableButton,
  Switch,
} from '../../Shared';
import React, { useEffect, useState } from 'react';
import { Divider, Header, Modal } from 'semantic-ui-react';
import YAML from 'yaml';

const Info = ({ state, theme }) => {
  const [contents, setContents] = useState();

  useEffect(() => {
    setTimeout(() => {
      setContents(
        YAML.stringify(state, { simpleKeys: true, sortMapEntries: false }),
      );
    }, 250);
  }, [state]);

  const { pendingRestart } = state;

  return (
    <>
      <div className="header-buttons">
        <div style={{ float: 'left' }}>
          <ShrinkableButton
            disabled={!contents}
            icon="refresh"
            mediaQuery="(max-width: 686px)"
            onClick={() => getVersion({ forceCheck: true })}
            primary
          >
            Check for Updates
          </ShrinkableButton>
          <ShrinkableButton
            color="yellow"
            disabled={!contents}
            icon="star"
            mediaQuery="(max-width: 686px)"
            onClick={() =>
              window.open(
                `http://www.slsknet.org/qtlogin.php?username=${state?.user?.username}`,
                '_blank',
              )
            }
          >
            Get Privileges
          </ShrinkableButton>
        </div>
        <Modal
          actions={[
            'Cancel',
            {
              content: 'Shut Down',
              key: 'done',
              negative: true,
              onClick: shutdown,
            },
          ]}
          centered
          content="Are you sure you want to shut the application down?  You'll need to manually start it again."
          header={
            <Header
              content="Confirm Shutdown"
              icon="redo"
            />
          }
          size="mini"
          trigger={
            <ShrinkableButton
              disabled={!contents}
              icon="shutdown"
              mediaQuery="(max-width: 686px)"
              negative
            >
              Shut Down
            </ShrinkableButton>
          }
        />
        <Modal
          actions={[
            'Cancel',
            {
              content: 'Restart',
              key: 'done',
              negative: true,
              onClick: restart,
            },
          ]}
          centered
          content="Are you sure you want restart the application?"
          header={
            <Header
              content="Confirm Restart"
              icon="redo"
            />
          }
          size="mini"
          trigger={
            <ShrinkableButton
              color={pendingRestart ? 'yellow' : undefined}
              disabled={!contents}
              icon="redo"
              mediaQuery="(max-width: 686px)"
              negative={!pendingRestart}
            >
              Restart
            </ShrinkableButton>
          }
        />
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
    </>
  );
};

export default Info;
