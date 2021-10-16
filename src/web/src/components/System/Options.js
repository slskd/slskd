import React, { Component } from 'react';

class Options extends Component {
  render = () => {
    return (
      <div>
        <pre>{JSON.stringify(this.props.options, null, 2)}</pre>
      </div>
    );
  }
}

export default Options;