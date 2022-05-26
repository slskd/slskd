import React from 'react';
import { useRouteMatch, useHistory, Redirect } from 'react-router-dom';

import { Segment, Tab } from 'semantic-ui-react';

import './System.css';

import Info from './Info';
import Logs from './Logs';
import Options from './Options';

const System = ({ state = {}, options = {} }) => {
  const { params: { tab }, ...route } = useRouteMatch();
  const history = useHistory();

  const panes = [
    { route: 'info', menuItem: 'Info', render: () => <Tab.Pane><Info state={state}/></Tab.Pane> },
    {
      route: 'options', menuItem: 'Options', render: () =>
        <Tab.Pane className='full-height'><Options options={options} /></Tab.Pane>,
    },
    { route: 'logs', menuItem: 'Logs', render: () => <Tab.Pane><Logs/></Tab.Pane> },
  ];

  const activeIndex = panes.findIndex(pane => pane.route === tab);

  const onTabChange = (e, { activeIndex }) => {
    history.push(panes[activeIndex].route);
  };

  if (tab === undefined) {
    return <Redirect to={`${route.url}/${panes[0].route}`}/>;
  }

  return (
    <div className='system'>
      <Segment raised>
        <Tab
          activeIndex={activeIndex > -1 ? activeIndex : 0} 
          panes={panes}
          onTabChange={onTabChange}
        />
      </Segment>
    </div>
  );
};

export default System;