import React from 'react';
import { Tab } from 'semantic-ui-react';

import './Files.css';
import Explorer from './Explorer';

const Files = ({ options } = {}) => {
  const { remoteFileManagement } = options;

  const panes = [
    { 
      route: 'downloads',
      menuItem: 'Downloads',
      render: () => <Tab.Pane><Explorer root={'downloads'} remoteFileManagement={remoteFileManagement}/></Tab.Pane>,
    },
    { 
      route: 'incomplete',
      menuItem: 'Incomplete',
      render: () => <Tab.Pane><Explorer root={'incomplete'} remoteFileManagement={remoteFileManagement}/></Tab.Pane>,
    },
  ];

  return (
    <div>
      <Tab
        panes={panes}
      />
    </div>
  );
};

export default Files;