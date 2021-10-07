import React, { Component } from 'react';
import { Segment, Tab } from 'semantic-ui-react';
import './System.css';

class Logs extends Component {
  componentDidMount = () => {
  }

  componentWillUnmount = () => {

  }

  render = () => {
    return (
      <div>
        <ul>
          <li>log 1 here</li>
          <li>log 2 here</li>
        </ul>
      </div>
    );
  }
}

export default Logs;