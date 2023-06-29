import React from 'react';
import { Tab } from 'semantic-ui-react';

const panes = [
  { 
    route: 'downloads',
    menuItem: 'Downloads',
    render: () => <Tab.Pane>Downloads</Tab.Pane>,
  },
  { 
    route: 'incomplete',
    menuItem: 'Incomplete',
    render: () => <Tab.Pane>Incomplete</Tab.Pane>,
  },
];

const Files = () => {
  return (
    <div>
      <Tab panes={panes}/>
    </div>
  );
};

export default Files;