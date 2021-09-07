import React, { Component } from 'react';
import * as transfers from '../../lib/transfers';
import PlaceholderSegment from '../Shared/PlaceholderSegment';

import TransferGroup from './TransferGroup';

class Transfers extends Component {
  state = { fetchState: '', downloads: [], interval: undefined }

  componentDidMount = () => {
    this.fetch();
    this.setState({ interval: window.setInterval(this.fetch, 500) });
  }

  componentWillUnmount = () => {
    clearInterval(this.state.interval);
    this.setState({ interval: undefined });
  }

  fetch = () => {
    this.setState({ fetchState: 'pending' }, async () => {
      try {
        const response = await transfers.getAll({ direction: this.props.direction })
        this.setState({ 
            fetchState: 'complete', downloads: response
        })
      } catch (err) {
        this.setState({ fetchState: 'failed' })
      }
    })
  }
  
  render = () => {
    const { downloads = [] } = this.state;
    const { direction } = this.props;

    return (
      downloads.length === 0 ? 
      <PlaceholderSegment icon={direction}/> :
      <div className='transfer-segment'>
        {downloads.map((user, index) => 
          <TransferGroup key={index} direction={this.props.direction} user={user}/>
        )}
      </div>
    );
  }
}

export default Transfers;