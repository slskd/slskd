import React, { Component } from 'react';
import * as transfers from '../../lib/transfers';

import {
  Card,
  Button,
  Icon,
} from 'semantic-ui-react';

import TransferList from './TransferList';

class TransferGroup extends Component {
  state = {
    selections: new Set(),
    isFolded: false,
  }

  onSelectionChange = (directoryName, file, selected) => {
    const {selections} = this.state;
    const obj = JSON.stringify({ directory: directoryName, filename: file.filename });
    selected ? selections.add(obj) : selections.delete(obj);

    this.setState({ selections });
  }

  isSelected = (directoryName, file) => 
    this.state.selections.has(JSON.stringify({ directory: directoryName, filename: file.filename }));

  getSelectedFiles = () => {
    const { user } = this.props;
        
    return Array.from(this.state.selections)
      .map(s => JSON.parse(s))
      .map(s => user.directories
        .find(d => d.directory === s.directory)
        .files.find(f => f.filename === s.filename)
      ).filter(s => s !== undefined);
  }

  removeFileSelection = (file) => {
    const {selections} = this.state;

    const match = Array.from(selections)
      .map(s => JSON.parse(s))
      .find(s => s.filename === file.filename);

    if (match) {
      selections.delete(JSON.stringify(match));
      this.setState({ selections });
    }
  }

  isStateRetryable = (state) =>
    this.props.direction === 'download' && state.includes('Completed') && state !== 'Completed, Succeeded';
  isStateCancellable = (state) =>
    ['InProgress', 'Requested', 'Queued', 'Queued, Remotely', 'Queued, Locally', 'Initializing'].find(s => s === state);
  isStateRemovable = (state) => state.includes('Completed');

  retryAll = async (selected) => {
    await Promise.all(selected.map(file => this.retry(file)));
  }

  cancelAll = async (direction, username, selected) => {
    await Promise.all(selected.map(file => transfers.cancel({ direction, username, id: file.id})));
  }

  removeAll = async (direction, username, selected) => {
    await Promise.all(selected.map(file => 
      transfers.cancel({ direction, username, id: file.id, remove: true })
        .then(() => this.removeFileSelection(file))));
  }

  retry = async (file) => {
    const { username, filename, size } = file;
        
    try {
      await transfers.download({username, files: [{filename, size }] });
    } catch (error) {
      console.log(error);
    }
  }

  fetchPlaceInQueue = async (file) => {
    const { username, id } = file;

    try {
      await transfers.getPlaceInQueue({ username, id });
    } catch (error) {
      console.log(error);
    }
  }

  toggleFolded = () => {
    this.setState({'isFolded': !this.state.isFolded});
  }

  render = () => {
    const { user, direction } = this.props;
    const {isFolded} = this.state;

    const selected = this.getSelectedFiles();
    const all = selected.length > 1 ? ' Selected' : '';
        
    const allRetryable = selected.filter(f => this.isStateRetryable(f.state)).length === selected.length;
    const anyCancellable = selected.filter(f => this.isStateCancellable(f.state)).length > 0;
    const allRemovable = selected.filter(f => this.isStateRemovable(f.state)).length === selected.length;

    return (
      <Card key={user.username} className='transfer-card' raised>
        <Card.Content>
          <Card.Header>
            <Icon
              link
              name={isFolded ? 'chevron right' : 'chevron down'}
              onClick={() => this.toggleFolded()}
            />
            {user.username}
          </Card.Header>
          {user.directories && !isFolded && user.directories
            .map((dir, index) => 
              <TransferList 
                key={index} 
                username={user.username} 
                directoryName={dir.directory}
                files={(dir.files || []).map(f => ({ ...f, selected: this.isSelected(dir.directory, f) }))}
                onSelectionChange={this.onSelectionChange}
                direction={this.props.direction}
                onPlaceInQueueRequested={this.fetchPlaceInQueue}
                onRetryRequested={this.retry}
              />
            )}
        </Card.Content>
        {selected && selected.length > 0 && 
                <Card.Content extra>
                  {<Button.Group>
                    {allRetryable && 
                        <Button 
                          icon='redo' 
                          color='green' 
                          content={`Retry${all}`} 
                          onClick={() => this.retryAll(selected)}
                        />}
                    {allRetryable && anyCancellable && <Button.Or/>}
                    {anyCancellable && 
                        <Button 
                          icon='x'
                          color='red'
                          content={`Cancel${all}`}
                          onClick={() => this.cancelAll(direction, user.username, selected)}
                        />}
                    {(allRetryable || anyCancellable) && allRemovable && <Button.Or/>}
                    {allRemovable && 
                        <Button 
                          icon='trash alternate'
                          content={`Remove${all}`}
                          onClick={() => this.removeAll(direction, user.username, selected)}
                        />}
                  </Button.Group>}
                </Card.Content>}
      </Card>
    );
  }
}

export default TransferGroup;
