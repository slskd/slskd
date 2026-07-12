import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Button, Icon, Input, Table } from 'semantic-ui-react';
import { useVirtualizer } from '@tanstack/react-virtual';

const ROW_H = 40;
const MIN_H = 60;
const MAX_H = 800;
const DEFAULT_H = 200;
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
    const { node, depth } = stack.pop();
    result.push({ node, depth });
    if (expandedPaths.has(node.name) && node.children?.length > 0) {
      for (let i = node.children.length - 1; i >= 0; i--) {
        stack.push({ node: node.children[i], depth: depth + 1 });
      }
    }
  }
  return result;
};

const filterTree = (tree, query) => {
  const lower = query.toLowerCase();

  const visit = (node, depth) => {
    const dirName = node.name.split(/[/\\]/).pop();
    const childResults = (node.children || []).flatMap((c) => visit(c, depth + 1));
    if (dirName.toLowerCase().includes(lower) || childResults.length > 0) {
      return [{ node, depth }, ...childResults];
    }
    return [];
  };

  return (tree || []).flatMap((root) => visit(root, 0));
};

const loadHeight = () => {
  const saved = parseInt(localStorage.getItem(HEIGHT_KEY), 10);
  return Number.isFinite(saved) ? Math.max(MIN_H, Math.min(MAX_H, saved)) : DEFAULT_H;
};

const DirectoryTree = ({ onSelect, selectedDirectoryName, tree }) => {
  const parentRef = useRef(null);
  const [expandedPaths, setExpandedPaths] = useState(new Set());
  const [searchInput, setSearchInput] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [treeHeight, setTreeHeight] = useState(loadHeight);

  // Keep a ref so the drag handler closure always sees the current height
  // without being recreated on every state change.
  const treeHeightRef = useRef(treeHeight);
  treeHeightRef.current = treeHeight;

  useEffect(() => {
    setExpandedPaths(new Set());
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

  const handleCollapseAll = useCallback(() => setExpandedPaths(new Set()), []);

  const handleResizeStart = useCallback((e) => {
    e.preventDefault();
    const startY = e.clientY;
    const startH = treeHeightRef.current;

    const clamp = (h) => Math.max(MIN_H, Math.min(MAX_H, h));

    const onMove = (me) => setTreeHeight(clamp(startH + me.clientY - startY));

    const onUp = (me) => {
      const finalH = clamp(startH + me.clientY - startY);
      setTreeHeight(finalH);
      localStorage.setItem(HEIGHT_KEY, String(finalH));
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  }, []);

  const items = useMemo(
    () =>
      searchQuery
        ? filterTree(tree, searchQuery)
        : flattenTree(tree, expandedPaths),
    [tree, searchQuery, expandedPaths],
  );

  const virtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => ROW_H,
    overscan: 15,
  });

  const visibleItems = virtualizer.getVirtualItems();
  const totalSize = virtualizer.getTotalSize();
  const containerH = Math.min(totalSize, treeHeight);
  const paddingTop = visibleItems.length > 0 ? visibleItems[0].start : 0;
  const paddingBottom =
    visibleItems.length > 0
      ? totalSize - visibleItems[visibleItems.length - 1].end
      : 0;

  return (
    <div>
      <div className="browse-folderlist-controls"
        style={{
          alignItems: 'center',
          display: 'flex',
          gap: '0.5em',
          marginBottom: '0.75em',
        }}
      >
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
          placeholder="Filter directories..."
          style={{ flex: 1 }}
          value={searchInput}
        />
        <Button
          basic
          compact
          icon="angle double down"
          onClick={handleExpandAll}
          size="tiny"
          title="Expand all"
        />
        <Button
          basic
          compact
          icon="angle double up"
          onClick={handleCollapseAll}
          size="tiny"
          title="Collapse all"
        />
      </div>
      <div
        ref={parentRef}
        style={{ height: `${containerH}px`, overflowX: 'auto', overflowY: 'auto' }}
      >
        <Table selectable style={{ minWidth: '100%' }}>
          <Table.Body>
            {paddingTop > 0 && (
              <Table.Row>
                <Table.Cell style={{ height: paddingTop, padding: 0 }} />
              </Table.Row>
            )}
            {visibleItems.map((vi) => {
              const { node, depth } = items[vi.index];
              const selected = node.name === selectedDirectoryName;
              const dirName = node.name.split(/[/\\]/).pop();
              const hasChildren = (node.children?.length ?? 0) > 0;
              const isExpanded = expandedPaths.has(node.name);
              const folderIcon = node.locked
                ? 'lock'
                : !hasChildren
                  ? 'folder outline'
                  : isExpanded || searchQuery
                    ? 'folder open'
                    : 'folder';
              const caption = formatCaption(node.totalFileCount, node.totalDirectoryCount);

              return (
                <Table.Row
                  key={vi.key}
                  onClick={(e) => onSelect(e, node)}
                  style={{ cursor: 'pointer' }}
                >
                  <Table.Cell
                    className="filelist-filename"
                    style={{
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
                      onClick={(e) => handleToggle(node, e)}
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
                      <span
                        style={{ fontSize: '0.8em', marginLeft: '0.5em', opacity: 0.5 }}
                      >
                        {caption}
                      </span>
                    )}
                  </Table.Cell>
                </Table.Row>
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
        onMouseDown={handleResizeStart}
        style={{ cursor: 'ns-resize', paddingBottom: '4px', paddingTop: '4px' }}
        title="Drag to resize"
      >
        <div
          style={{
            backgroundColor: 'rgba(128, 128, 128, 0.25)',
            borderRadius: '2px',
            height: '3px',
            margin: '0 auto',
            width: '48px',
          }}
        />
      </div>
    </div>
  );
};

export default DirectoryTree;
