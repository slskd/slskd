import {
  defaultHighlightStyle,
  StreamLanguage,
  syntaxHighlighting,
} from '@codemirror/language';
import { yaml } from '@codemirror/legacy-modes/mode/yaml';
import CodeMirror from '@uiw/react-codemirror';
import React from 'react';

const CodeEditor = ({ onChange = () => {}, theme, value, ...rest }) => {
  console.log(defaultHighlightStyle);

  return (
    <CodeMirror
      extensions={[
        StreamLanguage.define(yaml),
        syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
      ]}
      onChange={(newValue) => onChange(newValue)}
      theme={theme}
      value={value}
      {...rest}
    />
  );
};

export default CodeEditor;
