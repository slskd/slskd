import React, { useState, useEffect } from 'react';
import * as transfers from '../../lib/transfers';

import { formatBytes, getDirectoryName } from '../../lib/util';

import FileList from '../Shared/FileList';

import {
  Button,
  Card,
  Icon,
  Label,
} from 'semantic-ui-react';

const buildTree = (response) => {
  let { files = [], lockedFiles = [] } = response;
  files = files.concat(lockedFiles.map(file => ({ ...file, locked: true })));

  return files.reduce((dict, file) => {
    let dir = getDirectoryName(file.filename);
    let selectable = { selected: false, ...file };
    dict[dir] = dict[dir] === undefined ? [selectable] : dict[dir].concat(selectable);
    return dict;
  }, {});
};

/**
 * Functional component <Response>
 * 
 * @type {import('react').FunctionComponentElement<Props>} 
 */
export const Response = (props) => {

  /** State initialization */
  const [state, setState] = useState({
    tree: buildTree(props.response),
    downloadRequest: undefined,
    downloadError: '',
    isFolded: props.isInitiallyFolded,
  });

  /** Checking for changes in props.response and props.isInitiallyFolded, and updating state accordingly */
  useEffect(() => {
    if (props.response) {
      setState({ ...state, tree: buildTree(props.response) });
    }
    if (props.isInitiallyFolded) {
      setState({ ...state, isFolded: props.isInitiallyFolded });
    }

  }, [props.response, props.isInitiallyFolded, state]);

  /** 
   * Function to be called on download request, updates downloadRequest to 'inProgress',
   *  'complete' on download completion,
   *  or 'error' when error is caught 
   */
  const download = (username, files) => {
    setState({ ...state, downloadRequest: 'inProgress' }, async () => {
      try {
        const requests = (files || []).map(({ filename, size }) => ({ filename, size }));
        await transfers.download({ username, files: requests });

        setState({ ...state, downloadRequest: 'complete' });
      } catch (err) {
        setState({ ...state, downloadRequest: 'error', downloadError: err.response });
      }
    });
  };

  const onFileSelectionChange = (file, state) => {
    file.selected = state;
    setState({ ...state, tree: state.tree, downloadRequest: undefined, downloadError: '' });
  };

  const toggleFolded = () => {
    setState({ ...state, isFolded: !state.isFolded });
  };

  let { response } = props;
  let free = response.hasFreeUploadSlot;

  let { tree, downloadRequest, downloadError, isFolded } = state;

  let selectedFiles = Object.keys(tree)
    .reduce((list, dict) => list.concat(tree[dict]), [])
    .filter(f => f.selected);

  let selectedSize = formatBytes(selectedFiles.reduce((total, f) => total + f.size, 0));

  return (
    <Card className='result-card' raised>
      <Card.Content>
        <Card.Header>
          <Icon
            link
            name={isFolded ? 'chevron right' : 'chevron down'}
            onClick={toggleFolded}
          />
          <Icon name='circle' color={free ? 'green' : 'yellow'} />
          {response.username}
          <Icon
            className='close-button'
            name='close'
            color='red'
            link
            onClick={() => props.onHide()}
          />
        </Card.Header>
        <Card.Meta className='result-meta'>
          <span>
            Upload Speed: {formatBytes(response.uploadSpeed)}/s,
            Free Upload Slot: {free ? 'YES' : 'NO'}, Queue Length: {response.queueLength}</span>
        </Card.Meta>
        {((!isFolded && Object.keys(tree)) || []).map((dir, i) =>
          <FileList
            key={i}
            directoryName={dir}
            locked={tree[dir].find(file => file.locked)}
            files={tree[dir]}
            disabled={downloadRequest === 'inProgress'}
            onSelectionChange={onFileSelectionChange}
          />
        )}
      </Card.Content>
      {selectedFiles.length > 0 && <Card.Content extra>
        <span>
          <Button
            color='green'
            content='Download'
            icon='download'
            label={{
              as: 'a',
              basic: false,
              content: `${selectedFiles.length} file${selectedFiles.length === 1 ? '' : 's'}, ${selectedSize}`,
            }}
            labelPosition='right'
            onClick={() => download(response.username, selectedFiles)}
            disabled={props.disabled || downloadRequest === 'inProgress'}
          />
          {downloadRequest === 'inProgress' && <Icon loading name='circle notch' size='large' />}
          {downloadRequest === 'complete' && <Icon name='checkmark' color='green' size='large' />}
          {downloadRequest === 'error' && <span>
            <Icon name='x' color='red' size='large' />
            <Label>{downloadError.data + ` (HTTP ${downloadError.status} ${downloadError.statusText})`}</Label>
          </span>}
        </span>
      </Card.Content>}
    </Card>
  );
};