import React, { Component } from 'react';
import * as transfers from '../../lib/transfers';
import PlaceholderSegment from '../Shared/PlaceholderSegment';

import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';

import './Transfers.css';

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
          fetchState: 'complete', downloads: response,
        })
      } catch (err) {
        this.setState({ fetchState: 'failed' })
      }
    })
  }

  retry = async (file) => {
    const { username, filename, size } = file;
        
    try {
      await transfers.download({username, files: [{filename, size }] });
    } catch (error) {
      console.log(error);
    }
  }

  retryAll = async (transfers) => {
    console.log(transfers);
    // await Promise.all(transfers.map(file => this.retry(file)))
  }

  cancelAll = async (transfers) => {
    console.log(transfers);
  }

  removeall = async (transfers) => {
    console.log(transfers);
  }
  
  render = () => {
    const { downloads = [] } = this.state;
    const { direction } = this.props;

    return (
      <>
        <TransfersHeader 
          direction={direction} 
          transfers={downloads} 
          server={this.props.server}
          onRetryAll={this.removeall}
          onCancelAll={this.cancelAll}
          onRemoveAll={this.removeall}
        />
        {downloads.length === 0 
          ? <PlaceholderSegment icon={direction} caption={`No ${direction}s to display`}/>
          : downloads.map((user, index) => 
            <TransferGroup key={index} direction={this.props.direction} user={user}/>
          )}
      </>
    );
  }
}

export default Transfers;