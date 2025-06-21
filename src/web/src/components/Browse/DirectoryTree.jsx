import { useCallback, useEffect, useState } from 'react';
import { Button, List } from 'semantic-ui-react';

// Sort directories and files alphabetically, directories first
const sortTree = (nodes) => {
  if (!nodes) return [];

  const directories = nodes
    .filter((n) => n.children)
    .sort((a, b) => a.name.localeCompare(b.name));

  const files = nodes
    .filter((n) => !n.children)
    .sort((a, b) => a.name.localeCompare(b.name));

  return [...directories, ...files];
};

const DirectoryTree = ({ onSelect, selectedDirectoryName, tree }) => {
  // Only keep track of opened nodes at the first level
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

  const collapseAll = useCallback(() => {
    setOpened(new Set());
  }, []);

  // Only render the first level, and children if opened
  // eslint-disable-next-line complexity
  const renderNode = (d, level = 0) => {
    const selected = d.name === selectedDirectoryName;
    const isDirectory = Boolean(d.children);
    const hasChildren = isDirectory && d.children.length > 0;
    const isOpened = opened.has(d.name);
    const isLocked = d.locked === true;

    return (
      <List.Item key={d.name}>
        <div
          className="browse-folderlist-item-container"
          style={{ paddingLeft: `${level * 20}px` }}
        >
          {isDirectory && hasChildren && !isLocked ? (
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
              (isDirectory && hasChildren && !isLocked ? ' hoverable' : '')
            }
            name={
              isLocked
                ? 'lock'
                : selected
                  ? isDirectory
                    ? 'folder open'
                    : 'file outline'
                  : isDirectory
                    ? 'folder'
                    : 'file outline'
            }
            onClick={
              isDirectory && hasChildren && !isLocked
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

        {/* Only render children if this node is opened and it's a directory */}
        {isDirectory && hasChildren && isOpened && (
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
        <Button
          className="browse-collapse-all-button"
          compact
          onClick={collapseAll}
          size="mini"
        >
          Collapse All
        </Button>
      )}
      {tree && tree.length > 0 && (
        <List className="browse-folderlist-list">
          {sortTree(tree).map((d) => renderNode(d, 0))}
        </List>
      )}
    </div>
  );
};

export default DirectoryTree;
