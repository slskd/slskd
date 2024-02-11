import * as transfers from '../../lib/transfers';
import TransferList from './TransferList';
import React, { Component } from 'react';
import { Button, Card, Icon } from 'semantic-ui-react';

class TransferGroup extends Component {
  state = {
    isFolded: false,
    selections: new Set(),
  };

  onSelectionChange = (directoryName, file, selected) => {
    const { selections } = this.state;
    const object = JSON.stringify({
      directory: directoryName,
      filename: file.filename,
    });
    selected ? selections.add(object) : selections.delete(object);

    this.setState({ selections });
  };

  isSelected = (directoryName, file) =>
    this.state.selections.has(
      JSON.stringify({ directory: directoryName, filename: file.filename }),
    );

  getSelectedFiles = () => {
    const { user } = this.props;

    return Array.from(this.state.selections)
      .map((s) => JSON.parse(s))
      .map((s) =>
        user.directories
          .find((d) => d.directory === s.directory)
          .files.find((f) => f.filename === s.filename),
      )
      .filter((s) => s !== undefined);
  };

  removeFileSelection = (file) => {
    const { selections } = this.state;

    const match = Array.from(selections)
      .map((s) => JSON.parse(s))
      .find((s) => s.filename === file.filename);

    if (match) {
      selections.delete(JSON.stringify(match));
      this.setState({ selections });
    }
  };

  retryAll = async (selected) => {
    await Promise.all(selected.map((file) => this.retry(file)));
  };

  cancelAll = async (direction, username, selected) => {
    await Promise.all(
      selected.map((file) =>
        transfers.cancel({ direction, id: file.id, username }),
      ),
    );
  };

  removeAll = async (direction, username, selected) => {
    await Promise.all(
      selected.map((file) =>
        transfers
          .cancel({ direction, id: file.id, remove: true, username })
          .then(() => this.removeFileSelection(file)),
      ),
    );
  };

  retry = async (file) => {
    const { filename, size, username } = file;

    try {
      await transfers.download({ files: [{ filename, size }], username });
    } catch (error) {
      console.error(error);
    }
  };

  fetchPlaceInQueue = async (file) => {
    const { id, username } = file;

    try {
      await transfers.getPlaceInQueue({ id, username });
    } catch (error) {
      console.error(error);
    }
  };

  toggleFolded = () => {
    this.setState({ isFolded: !this.state.isFolded });
  };

  render() {
    const { direction, user } = this.props;
    const { isFolded } = this.state;

    const selected = this.getSelectedFiles();
    const all = selected.length > 1 ? ' Selected' : '';

    const allRetryable =
      selected.filter((f) => transfers.isStateRetryable(f.state)).length ===
      selected.length;
    const anyCancellable = selected.some((f) =>
      transfers.isStateCancellable(f.state),
    );
    const allRemovable =
      selected.filter((f) => transfers.isStateRemovable(f.state)).length ===
      selected.length;

    return (
      <Card
        className="transfer-card"
        key={user.username}
        raised
      >
        <Card.Content>
          <Card.Header>
            <Icon
              link
              name={isFolded ? 'chevron right' : 'chevron down'}
              onClick={() => this.toggleFolded()}
            />
            {user.username}
          </Card.Header>
          {user.directories &&
            !isFolded &&
            user.directories.map((dir, index) => (
              <TransferList
                direction={this.props.direction}
                directoryName={dir.directory}
                files={(dir.files || []).map((f) => ({
                  ...f,
                  selected: this.isSelected(dir.directory, f),
                }))}
                key={index}
                onPlaceInQueueRequested={this.fetchPlaceInQueue}
                onRetryRequested={this.retry}
                onSelectionChange={this.onSelectionChange}
                username={user.username}
              />
            ))}
        </Card.Content>
        {selected && selected.length > 0 && (
          <Card.Content extra>
            <Button.Group>
              {allRetryable && (
                <Button
                  color="green"
                  content={`Retry${all}`}
                  icon="redo"
                  onClick={() => this.retryAll(selected)}
                />
              )}
              {allRetryable && anyCancellable && <Button.Or />}
              {anyCancellable && (
                <Button
                  color="red"
                  content={`Cancel${all}`}
                  icon="x"
                  onClick={() =>
                    this.cancelAll(direction, user.username, selected)
                  }
                />
              )}
              {(allRetryable || anyCancellable) && allRemovable && (
                <Button.Or />
              )}
              {allRemovable && (
                <Button
                  content={`Remove${all}`}
                  icon="trash alternate"
                  onClick={() =>
                    this.removeAll(direction, user.username, selected)
                  }
                />
              )}
            </Button.Group>
          </Card.Content>
        )}
      </Card>
    );
  }
}

export default TransferGroup;
