import { useCallback, useEffect, useState } from 'react';
import { List } from 'semantic-ui-react';

const sortTree = (nodes) => {
  return nodes
    .filter((n) => n.children)
    .sort((a, b) => a.name.localeCompare(b.name));
};

const DirectoryTree = ({ onSelect, selectedDirectoryName, tree }) => {
  const [opened, setOpened] = useState(new Set());

  useEffect(() => {
    setOpened(new Set());
  }, [tree]);

  const toggleCollapse = useCallback((directoryName) => {
    setOpened((previousOpened) => {
      const newOpened = new Set(previousOpened);
      if (newOpened.has(directoryName)) {
        newOpened.delete(directoryName);
      } else {
        newOpened.add(directoryName);
      }

      return newOpened;
    });
  }, []);

  // eslint-disable-next-line complexity
  const renderNode = (d, level = 0) => {
    const selected = d.name === selectedDirectoryName;
    const hasChildren = d.children.length > 0;
    const isOpened = opened.has(d.name);
    const isLocked = d.locked === true;

    return (
      <List.Item key={d.name}>
        <div
          className="browse-folderlist-item-container"
          style={{ paddingLeft: `${level * 20}px` }}
        >
          {hasChildren && !isLocked ? (
            <List.Icon
              className={`browse-folderlist-expand-icon ${isOpened ? 'expanded' : 'collapsed'}`}
              name={isOpened ? 'caret down' : 'caret right'}
              onClick={() => toggleCollapse(d.name)}
            />
          ) : (
            <div className="browse-folderlist-expand-spacer" />
          )}
          <List.Icon
            className={
              'browse-folderlist-icon' +
              (selected ? ' selected' : '') +
              (isLocked ? ' locked' : '') +
              (hasChildren && !isLocked ? ' hoverable' : '')
            }
            name={isLocked ? 'lock' : selected ? 'folder open' : 'folder'}
            onClick={
              hasChildren && !isLocked
                ? () => toggleCollapse(d.name)
                : undefined
            }
          />
          <List.Header
            className={
              'browse-folderlist-header' +
              (selected ? ' selected' : '') +
              (isLocked ? ' locked' : '')
            }
            onClick={(event) => onSelect(event, d)}
          >
            {d.name.split('\\').pop().split('/').pop()}
          </List.Header>
        </div>

        {hasChildren && isOpened && (
          <List.List>
            {sortTree(d.children).map((child) => renderNode(child, level + 1))}
          </List.List>
        )}
      </List.Item>
    );
  };

  return (
    <div className="browse-directorytree-container">
      {tree && tree.length > 0 && (
        <List className="browse-folderlist-list">
          {sortTree(tree).map((d) => renderNode(d, 0))}
        </List>
      )}
    </div>
  );
};

export default DirectoryTree;
