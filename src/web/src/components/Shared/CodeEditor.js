import React, {useEffect, useState}  from 'react';

import CodeMirror from '@uiw/react-codemirror';
import { StreamLanguage, syntaxHighlighting, defaultHighlightStyle  } from '@codemirror/language';
import { yaml } from '@codemirror/legacy-modes/mode/yaml';

const CodeEditor = ({ value, onChange = () => {}, ...rest}) => {
  console.log(defaultHighlightStyle);
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const initialTheme = prefersDark ? 'dark' : 'light';
  const [theme, setTheme] = useState(initialTheme);
  useEffect(() => {
    window.matchMedia('(prefers-color-scheme: dark)')
      .addEventListener('change', (e) => e.matches && setTheme('dark'));
    window.matchMedia('(prefers-color-scheme: light)')
      .addEventListener('change', (e) => e.matches && setTheme('light'));
  }, []);

  return (
    <CodeMirror
      value={value}
      extensions={[
        StreamLanguage.define(yaml),  
        syntaxHighlighting(defaultHighlightStyle, {fallback: true})]}
      onChange={(value, _) => onChange(value)}
      theme={theme}
      { ...rest}
    />
  );
};

export default CodeEditor;