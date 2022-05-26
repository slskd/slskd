import React from 'react';

import CodeMirror from '@uiw/react-codemirror';
import { StreamLanguage  } from '@codemirror/stream-parser';
import { yaml } from '@codemirror/legacy-modes/mode/yaml';
import { defaultHighlightStyle } from '@codemirror/highlight';

const CodeEditor = ({ value, onChange = () => {}, ...rest}) => {
  return (
    <CodeMirror
      value={value}
      extensions={[StreamLanguage.define(yaml), defaultHighlightStyle.fallback]}
      onChange={(value, _) => onChange(value)}
      { ...rest}
    />
  );
};

export default CodeEditor;