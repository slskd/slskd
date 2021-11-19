import React, { Component } from 'react';
import { Segment, Tab } from 'semantic-ui-react';
import './System.css';
import Info from './Info';
import Logs from './Logs';
import Options from './Options';

class System extends Component {
  componentDidMount = () => {
  }

  componentWillUnmount = () => {

  }

  render = () => {
    return (
      <div className='system'>
        <Segment raised>
          <Tab panes={[
            { menuItem: 'Info', render: () => <Tab.Pane><Info state={this.props.state}/></Tab.Pane> },
            { menuItem: 'Options', render: () => <Tab.Pane className='full-height'><Options options={this.props.options}/></Tab.Pane> },
            { menuItem: 'Logs', render: () => <Tab.Pane><Logs/></Tab.Pane> },
          ]}/>
        </Segment>
      </div>
    );
  }
}

export default System;