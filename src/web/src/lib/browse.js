// shared processing for browse responses, used by the Browse page and the user panel

export const buildDirectoryTree = ({ directories, separator }) => {
  if (!directories.length || directories[0].name === undefined) {
    return [];
  }

  // group each directory under its parent path in a single O(N) pass
  const byParent = new Map();
  const nameSet = new Set();

  for (const d of directories) {
    nameSet.add(d.name);
    const lastSep = d.name.lastIndexOf(separator);
    const parentKey = lastSep === -1 ? '' : d.name.slice(0, lastSep);
    let bucket = byParent.get(parentKey);
    if (!bucket) {
      bucket = [];
      byParent.set(parentKey, bucket);
    }

    bucket.push(d);
  }

  // roots are directories whose parent path isn't itself in the list
  const roots = directories.filter((d) => {
    const lastSep = d.name.lastIndexOf(separator);
    const parentKey = lastSep === -1 ? '' : d.name.slice(0, lastSep);
    return !nameSet.has(parentKey);
  });

  // recursively build the tree, computing file/directory counts along the way
  const buildNode = (dir) => {
    const children = (byParent.get(dir.name) || []).map(buildNode);
    return {
      ...dir,
      children,
      totalDirectoryCount:
        children.length +
        children.reduce((s, c) => s + c.totalDirectoryCount, 0),
      totalFileCount:
        (dir.files?.length ?? 0) +
        children.reduce((s, c) => s + c.totalFileCount, 0),
    };
  };

  return roots.map(buildNode);
};

export const findDirectoryByPath = (path, nodes) => {
  for (const node of nodes) {
    if (node.name === path) {
      return node;
    }

    const found = findDirectoryByPath(path, node.children || []);

    if (found) {
      return found;
    }
  }

  return null;
};

// flattens locked directories into the list, detects the path separator and
// tallies counts for display
export const processBrowseResponse = (response) => {
  const { directories = [], lockedDirectories = [] } = response;

  // detect the path separator from the first directory name we see
  let separator;
  const fileCount = directories.reduce((accumulator, directory) => {
    if (!separator) {
      if (directory.name.includes('\\')) separator = '\\';
      else if (directory.name.includes('/')) separator = '/';
    }

    return accumulator + directory.fileCount;
  }, 0);

  const lockedFileCount = lockedDirectories.reduce(
    (accumulator, directory) => accumulator + directory.fileCount,
    0,
  );

  return {
    directories: directories.concat(
      lockedDirectories.map((d) => ({ ...d, locked: true })),
    ),
    info: {
      directories: directories.length,
      files: fileCount,
      lockedDirectories: lockedDirectories.length,
      lockedFiles: lockedFileCount,
    },
    separator,
  };
};

export const formatBrowseSummary = ({
  directories,
  files,
  lockedDirectories,
  lockedFiles,
}) =>
  `${files + lockedFiles} files in ${directories + lockedDirectories} ` +
  `directories (including ${lockedFiles} files in ${lockedDirectories} locked directories)`;
