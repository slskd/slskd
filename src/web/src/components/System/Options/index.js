import React, { useState } from 'react';

import Edit from './Edit';
import View from './View';

const Index = ({ options }) => {
  const [mode, setMode] = useState('view');

  if (mode === 'edit') {
    return <Edit cancelAction={() => setMode('view')}/>
  }

  return <View options={options} editAction={() => setMode('edit')}/>
}

export default Index;