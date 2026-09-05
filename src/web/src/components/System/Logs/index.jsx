import History from './History';
import Live from './Live';
import React from 'react';
import { Tab } from 'semantic-ui-react';

const Logs = () => {
  const panes = [
    {
      menuItem: 'Live',
      render: () => (
        <Tab.Pane>
          <Live />
        </Tab.Pane>
      ),
      route: 'live',
    },
    {
      menuItem: 'History',
      render: () => (
        <Tab.Pane>
          <History />
        </Tab.Pane>
      ),
      route: 'history',
    },
  ];

  return (
    <div>
      <Tab panes={panes} />
    </div>
  );
};

export default Logs;
