import { useCallback, useState } from 'react';
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
  // Initialize with all directories collapsed
  const [collapsed, setCollapsed] = useState(() => {
    const getAllDirectoryNames = (directories) => {
      const names = new Set();

      const traverse = (directories_) => {
        for (const directory of directories_ || []) {
          if (directory.children) {
            names.add(directory.name);
            traverse(directory.children);
          }
        }
      };

      traverse(directories || []);
      return names;
    };

    return getAllDirectoryNames(tree);
  });

  const toggleCollapse = useCallback((directoryName) => {
    setCollapsed((previous) => {
      const newCollapsed = new Set(previous);
      if (newCollapsed.has(directoryName)) {
        newCollapsed.delete(directoryName);
      } else {
        newCollapsed.add(directoryName);
      }

      return newCollapsed;
    });
  }, []);

  const collapseAll = useCallback(() => {
    const getAllDirectoryNames = (directories) => {
      const names = new Set();

      const traverse = (directories_) => {
        for (const directory of directories_ || []) {
          if (directory.children) {
            names.add(directory.name);
            traverse(directory.children);
          }
        }
      };

      traverse(directories);
      return names;
    };

    setCollapsed(getAllDirectoryNames(tree || []));
  }, [tree]);

  // eslint-disable-next-line complexity
  const renderNode = (
    d,
    selectedDirectoryNameParameter,
    onSelectParameter,
    collapsedParameter,
    toggleCollapseParameter,
    level,
  ) => {
    const selected = d.name === selectedDirectoryNameParameter;
    const isDirectory = Boolean(d.children);
    const hasChildren = isDirectory && d.children.length > 0;
    const isCollapsed = collapsedParameter.has(d.name);
    const isLocked = d.locked === true;

    return (
      <List
        className="browse-folderlist-list"
        key={d.name}
      >
        <List.Item>
          <div
            className="browse-folderlist-item-container"
            style={{ paddingLeft: `${level * 20}px` }}
          >
            {isDirectory && hasChildren && !isLocked ? (
              <List.Icon
                className={`browse-folderlist-expand-icon ${isCollapsed ? 'collapsed' : 'expanded'}`}
                name={isCollapsed ? 'caret right' : 'caret down'}
                onClick={() => toggleCollapseParameter(d.name)}
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
                  ? () => toggleCollapseParameter(d.name)
                  : undefined
              }
            />
            <List.Header
              className={
                'browse-folderlist-header' +
                (selected ? ' selected' : '') +
                (isLocked ? ' locked' : '')
              }
              onClick={(event) => onSelectParameter(event, d)}
            >
              {d.name.split('\\').pop().split('/').pop()}
            </List.Header>
          </div>
          <List.Content>
            {isDirectory && hasChildren && !isCollapsed && (
              <List.List>
                {d.children.map((child) =>
                  renderNode(
                    child,
                    selectedDirectoryNameParameter,
                    onSelectParameter,
                    collapsedParameter,
                    toggleCollapseParameter,
                    level + 1,
                  ),
                )}
              </List.List>
            )}
          </List.Content>
        </List.Item>
      </List>
    );
  };

  const subtree = (
    root,
    selectedDirectoryNameParameter,
    onSelectParameter,
    collapsedParameter,
    toggleCollapseParameter,
    level = 0,
  ) =>
    sortTree(root).map((d) =>
      renderNode(
        d,
        selectedDirectoryNameParameter,
        onSelectParameter,
        collapsedParameter,
        toggleCollapseParameter,
        level,
      ),
    );

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
      {subtree(
        tree,
        selectedDirectoryName,
        onSelect,
        collapsed,
        toggleCollapse,
      )}
    </div>
  );
};

export default DirectoryTree;
