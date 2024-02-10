import React from 'react';

import { 
  List,
} from 'semantic-ui-react';

const subtree = (root, selectedDirectoryName, onSelect) => {
  return (root || []).map((d, index) => {
    const selected = d.name === selectedDirectoryName;
    // const dimIfLocked = { opacity: d.locked ? 0.5 : 1 };

    return (
      <List key={index} className='browse-folderlist-list'>
        <List.Item>
          <List.Icon 
            className={'browse-folderlist-icon' + (selected ? ' selected' : '') + (d.locked ? ' locked' : '')}
            name={d.locked === true ? 'lock' : selected ? 'folder open' : 'folder'}
          />
          <List.Content>
            <List.Header 
              className={'browse-folderlist-header' + (selected ? ' selected' : '') + (d.locked ? ' locked' : '')}
              onClick={(event) => onSelect(event, d)}
            >
              {d.name.split('\\').pop().split('/').pop()}
            </List.Header>
            <List.List>
              {subtree(d.children, selectedDirectoryName, onSelect)}
            </List.List>
          </List.Content>
        </List.Item>
      </List>);
  });
};

const DirectoryTree = ({ tree, selectedDirectoryName, onSelect }) => subtree(tree, selectedDirectoryName, onSelect);

export default DirectoryTree;