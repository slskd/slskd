import { useVirtualizer } from '@tanstack/react-virtual';
import React, {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Button, Icon, Input, Table } from 'semantic-ui-react';

const ROW_H = 33;
const MIN_H = ROW_H;
const DEFAULT_H = 300;
const HEIGHT_STEP = 20;
const HEIGHT_KEY = 'slskd-browse-tree-height';

const formatCaption = (fileCount, dirCount) => {
  const parts = [];
  if (fileCount > 0) {
    parts.push(`${fileCount} file${fileCount === 1 ? '' : 's'}`);
  }

  if (dirCount > 0) {
    parts.push(`${dirCount} director${dirCount === 1 ? 'y' : 'ies'}`);
  }

  return parts.join(', ');
};

const flattenTree = (tree, expandedPaths) => {
  const result = [];
  const stack = [];
  for (let i = (tree || []).length - 1; i >= 0; i--) {
    stack.push({ node: tree[i], depth: 0 });
  }

  while (stack.length > 0) {
    const { depth, node } = stack.pop();
    result.push({ node, depth });
    if (expandedPaths.has(node.name) && node.children?.length > 0) {
      for (let i = node.children.length - 1; i >= 0; i--) {
        stack.push({ node: node.children[i], depth: depth + 1 });
      }
    }
  }

  return result;
};

// path of nodes from the root to the node named targetName, or null
const findNodePath = (nodes, targetName) => {
  for (const node of nodes || []) {
    if (node.name === targetName) {
      return [node];
    }

    const childPath = findNodePath(node.children, targetName);
    if (childPath) {
      return [node, ...childPath];
    }
  }

  return null;
};

const filterTree = (tree, query) => {
  const lower = query.toLowerCase();

  const visit = (node, depth) => {
    const dirName = node.name.split(/[/\\]/u).pop();
    const childResults = (node.children || []).flatMap((c) =>
      visit(c, depth + 1),
    );
    if (dirName.toLowerCase().includes(lower) || childResults.length > 0) {
      return [{ node, depth }, ...childResults];
    }

    return [];
  };

  return (tree || []).flatMap((root) => visit(root, 0));
};

const loadHeight = () => {
  const saved = Number.parseInt(localStorage.getItem(HEIGHT_KEY), 10);
  return Number.isFinite(saved) ? Math.max(MIN_H, saved) : DEFAULT_H;
};

const clampHeight = (h) => Math.max(MIN_H, h);

const TreeRow = ({
  depth,
  hasNoTopBorder,
  isExpanded,
  isSearching,
  node,
  onSelect,
  onToggle,
  selected,
}) => {
  const dirName = node.name.split(/[/\\]/u).pop();
  const hasChildren = (node.children?.length ?? 0) > 0;
  const folderIcon = node.locked
    ? 'lock'
    : !hasChildren
      ? 'folder outline'
      : isExpanded || isSearching
        ? 'folder open'
        : 'folder';
  const caption = formatCaption(node.totalFileCount, node.totalDirectoryCount);

  return (
    <Table.Row
      className="browse-folderlist-row"
      onClick={(e) => onSelect(e, node)}
    >
      <Table.Cell
        className="filelist-filename"
        style={{
          // avoids doubling up with the container's own top border
          borderTop: hasNoTopBorder ? 'none' : undefined,
          paddingLeft: depth > 0 ? `${depth * 2}em` : undefined,
          whiteSpace: 'nowrap',
        }}
      >
        <Icon
          className={[
            'browse-folderlist-icon',
            selected ? 'selected' : null,
            node.locked ? 'locked' : null,
          ]
            .filter(Boolean)
            .join(' ')}
          name={folderIcon}
          onClick={(e) => onToggle(node, e)}
        />
        <span
          className={[
            'browse-folderlist-header',
            selected ? 'selected' : null,
            node.locked ? 'locked' : null,
          ]
            .filter(Boolean)
            .join(' ')}
        >
          {dirName}
        </span>
        {caption && (
          <span className="browse-folderlist-caption">{caption}</span>
        )}
      </Table.Cell>
    </Table.Row>
  );
};

const DirectoryTree = forwardRef(
  ({ onSelect, selectedDirectoryName, tree }, ref) => {
    const parentRef = useRef(null);
    const [expandedPaths, setExpandedPaths] = useState(new Set());
    const [searchInput, setSearchInput] = useState('');
    const [searchQuery, setSearchQuery] = useState('');
    const [treeHeight, setTreeHeight] = useState(loadHeight);
    const [scrollTarget, setScrollTarget] = useState(null);

    // lets the drag handler read the latest height without being recreated
    // on every resize
    const treeHeightRef = useRef(treeHeight);
    treeHeightRef.current = treeHeight;

    useEffect(() => {
      // expand the first root by default as a hint that folders can be toggled
      const firstRoot = tree?.[0];
      setExpandedPaths(
        firstRoot?.children?.length > 0 ? new Set([firstRoot.name]) : new Set(),
      );
      setSearchInput('');
      setSearchQuery('');
    }, [tree]);

    useEffect(() => {
      const timer = setTimeout(() => setSearchQuery(searchInput), 200);
      return () => clearTimeout(timer);
    }, [searchInput]);

    const handleToggle = useCallback((node, e) => {
      e.stopPropagation();
      if (!node.children?.length) {
        return;
      }

      setExpandedPaths((prev) => {
        const next = new Set(prev);
        if (next.has(node.name)) {
          next.delete(node.name);
        } else {
          next.add(node.name);
        }

        return next;
      });
    }, []);

    const handleExpandAll = useCallback(() => {
      const paths = new Set();
      const visit = (nodes) => {
        for (const node of nodes) {
          if (node.children?.length > 0) {
            paths.add(node.name);
            visit(node.children);
          }
        }
      };

      visit(tree || []);
      setExpandedPaths(paths);
    }, [tree]);

    const handleCollapseAll = useCallback(
      () => setExpandedPaths(new Set()),
      [],
    );

    // expands every ancestor of the given directory, selects it, and queues
    // a scroll so it ends up centered once it renders
    const navigateToDirectory = useCallback(
      (directoryName) => {
        const path = findNodePath(tree, directoryName);
        if (!path) {
          return;
        }

        const targetNode = path[path.length - 1];
        const ancestorNames = path.slice(0, -1).map((node) => node.name);

        setSearchInput('');
        setSearchQuery('');
        setExpandedPaths((prev) => new Set([...prev, ...ancestorNames]));
        setScrollTarget(directoryName);
        onSelect(undefined, targetNode);
      },
      [onSelect, tree],
    );

    useImperativeHandle(ref, () => ({ navigateToDirectory }), [
      navigateToDirectory,
    ]);

    const persistHeight = useCallback((h) => {
      setTreeHeight(h);
      localStorage.setItem(HEIGHT_KEY, String(h));
    }, []);

    const handleResizeStart = useCallback(
      (e) => {
        e.preventDefault();
        const startY = e.clientY;
        const startH = treeHeightRef.current;

        const onMove = (me) =>
          setTreeHeight(clampHeight(startH + me.clientY - startY));

        const onUp = (me) => {
          persistHeight(clampHeight(startH + me.clientY - startY));
          document.removeEventListener('mousemove', onMove);
          document.removeEventListener('mouseup', onUp);
        };

        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
      },
      [persistHeight],
    );

    const handleResizeKeyDown = useCallback(
      (e) => {
        if (e.key === 'ArrowUp') {
          persistHeight(clampHeight(treeHeightRef.current - HEIGHT_STEP));
        } else if (e.key === 'ArrowDown') {
          persistHeight(clampHeight(treeHeightRef.current + HEIGHT_STEP));
        }
      },
      [persistHeight],
    );

    const items = useMemo(
      () =>
        searchQuery
          ? filterTree(tree, searchQuery)
          : flattenTree(tree, expandedPaths),
      [expandedPaths, searchQuery, tree],
    );

    const virtualizer = useVirtualizer({
      count: items.length,
      getScrollElement: () => parentRef.current,
      estimateSize: () => ROW_H,
      overscan: 15,
    });

    useEffect(() => {
      if (!scrollTarget) {
        return;
      }

      const index = items.findIndex((item) => item.node.name === scrollTarget);
      if (index !== -1) {
        virtualizer.scrollToIndex(index, { align: 'center' });
        setScrollTarget(null);
      }
    }, [items, scrollTarget, virtualizer]);

    const visibleItems = virtualizer.getVirtualItems();
    const totalSize = virtualizer.getTotalSize();
    const paddingTop = visibleItems.length > 0 ? visibleItems[0].start : 0;
    const paddingBottom =
      visibleItems.length > 0
        ? totalSize - visibleItems[visibleItems.length - 1].end
        : 0;

    return (
      <div>
        <div className="browse-folderlist-controls">
          <Input
            action={
              Boolean(searchInput) && {
                color: 'red',
                icon: 'x',
                onClick: () => setSearchInput(''),
              }
            }
            label={{ content: 'Filter', icon: 'filter' }}
            onChange={(_, { value }) => setSearchInput(value)}
            placeholder="lackluster container"
            value={searchInput}
          />
          <Button
            icon="chevron down"
            onClick={handleExpandAll}
            title="Expand all"
          />
          <Button
            icon="chevron up"
            onClick={handleCollapseAll}
            title="Collapse all"
          />
          <Button
            disabled={!selectedDirectoryName}
            icon="bullseye"
            onClick={() => navigateToDirectory(selectedDirectoryName)}
            title="Show selected directory"
          />
        </div>
        <div
          className="browse-folderlist-scroll"
          ref={parentRef}
          style={{ height: `${treeHeight}px` }}
        >
          <Table selectable>
            <Table.Body>
              {paddingTop > 0 && (
                <Table.Row>
                  <Table.Cell style={{ height: paddingTop, padding: 0 }} />
                </Table.Row>
              )}
              {visibleItems.map((vi, visibleIndex) => {
                const { depth, node } = items[vi.index];
                return (
                  <TreeRow
                    depth={depth}
                    hasNoTopBorder={visibleIndex === 0}
                    isExpanded={expandedPaths.has(node.name)}
                    isSearching={Boolean(searchQuery)}
                    key={vi.key}
                    node={node}
                    onSelect={onSelect}
                    onToggle={handleToggle}
                    selected={node.name === selectedDirectoryName}
                  />
                );
              })}
              {paddingBottom > 0 && (
                <Table.Row>
                  <Table.Cell style={{ height: paddingBottom, padding: 0 }} />
                </Table.Row>
              )}
            </Table.Body>
          </Table>
        </div>
        <div
          aria-label="Resize the directory tree"
          aria-orientation="horizontal"
          aria-valuemin={MIN_H}
          aria-valuenow={treeHeight}
          className="browse-folderlist-resize-handle"
          onKeyDown={handleResizeKeyDown}
          onMouseDown={handleResizeStart}
          role="slider"
          tabIndex={0}
          title="Drag or use arrow keys to resize"
        >
          <div className="browse-folderlist-resize-grip">
            <span />
            <span />
            <span />
          </div>
        </div>
      </div>
    );
  },
);

DirectoryTree.displayName = 'DirectoryTree';

export default DirectoryTree;
