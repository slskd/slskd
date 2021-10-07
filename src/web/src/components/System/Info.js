import React, { Component } from 'react';

class Info extends Component {
  render = () => {
    return (
      <div>
        <pre>{JSON.stringify(this.props.state, null, 2)}</pre>
      </div>
    );
  }
}

export default Info;