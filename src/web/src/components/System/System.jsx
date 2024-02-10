import React from 'react';
import { useRouteMatch, useHistory, Redirect } from 'react-router-dom';

import { Segment, Tab, Menu, Icon } from 'semantic-ui-react';

import './System.css';

import { Switch } from '../Shared';
import Info from './Info';
import Logs from './Logs';
import Options from './Options';
import Shares from './Shares';
import Files from './Files';
import Data from './Data';

const System = ({ state = {}, theme, options = {} }) => {
  const { params: { tab }, ...route } = useRouteMatch();
  const history = useHistory();

  const panes = [
    { 
      route: 'info',
      menuItem: (<Menu.Item key='info'>
        <Switch
          pending={((state?.pendingRestart ?? false) || (state?.pendingReconnect ?? false)) 
            && <Icon name='exclamation circle' color='yellow'/>}
        >
          <Icon name='info circle'/>
        </Switch>
        Info
      </Menu.Item>), 
      render: () => <Tab.Pane><Info state={state} theme={theme}/></Tab.Pane>, 
    },
    {
      route: 'options', 
      menuItem: { 
        key: 'options', 
        icon: 'options', 
        content: 'Options', 
      }, 
      render: () => <Tab.Pane className='full-height'><Options options={options} theme={theme}/></Tab.Pane>,
    },
    {
      route: 'shares', 
      menuItem: (<Menu.Item key='shares'>
        <Switch
          scanPending={(state?.shares?.scanPending ?? false) && <Icon name='exclamation circle' color='yellow'/>}
        >
          <Icon name='share external'/>
        </Switch>
        Shares
      </Menu.Item>),
      render: () => <Tab.Pane><Shares state={state.shares} theme={theme}/></Tab.Pane>,
    },
    {
      route: 'files',
      menuItem: {
        key: 'files',
        icon: 'folder open',
        content: 'Files',
      },
      render: () => <Tab.Pane className='full-height'><Files options={options} theme={theme}/></Tab.Pane>,
    },
    {
      route: 'data',
      menuItem: {
        key: 'data',
        icon: 'database',
        content: 'Data',
      },
      render: () => <Tab.Pane className='full-height'><Data theme={theme}/></Tab.Pane>,
    },
    { 
      route: 'logs', 
      menuItem: { 
        key: 'logs', 
        icon: 'file outline', 
        content: 'Logs', 
      }, 
      render: () => <Tab.Pane><Logs/></Tab.Pane>, 
    },
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