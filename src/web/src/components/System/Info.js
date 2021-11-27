import React from 'react';
import YAML from 'yaml';

import CodeEditor from '../Shared/CodeEditor';

const Info = ({ state }) => {
  const stateAsYaml = YAML.stringify(state, { simpleKeys: true, sortMapEntries: true })

  return (
    <div className='state-code-container'>
      <CodeEditor
        value={stateAsYaml}
        basicSetup={false}
        editable={false}
      />
    </div>
  );
}

export default Info;