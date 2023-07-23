import React, { useState } from 'react';
import { Tab } from 'semantic-ui-react';

import './Files.css';
import Explorer from './Explorer';

const Files = () => {
  const [tab, setTab] = useState('downloads');

  const panes = [
    { 
      route: 'downloads',
      menuItem: 'Downloads',
      render: () => <Tab.Pane><Explorer root={'downloads'} isActive={tab === 'incomplete'}/></Tab.Pane>,
    },
    { 
      route: 'incomplete',
      menuItem: 'Incomplete',
      render: () => <Tab.Pane><Explorer root={'incomplete'} isActive={tab === 'incomplete'}/></Tab.Pane>,
    },
  ];

  const onTabChange = (e, { activeIndex }) => {
    setTab(panes[activeIndex].route);
  };

  return (
    <div>
      <Tab
        panes={panes}
        onTabChange={onTabChange}
      />
    </div>
  );
};

export default Files;