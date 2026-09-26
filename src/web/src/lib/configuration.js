// helpers for making targeted edits to the YAML configuration file without
// disturbing anything else in it; comments, ordering, and keys we don't know
// about all survive a round trip.
import { getYaml, updateYaml, validateYaml } from './options';
import YAML from 'yaml';
import { Pair, Scalar, YAMLMap } from 'yaml/types';

// the server matches keys case-insensitively and ignores underscores and
// dashes, so 'listenPort', 'listen_port' and 'listen-port' are the same key
const normalizeKey = (key) =>
  String(key?.value ?? key)
    .toLowerCase()
    .replaceAll(/[_-]/gu, '');

// options arrive from the api in camelCase; the config file convention is snake_case
export const toYamlKey = (key) =>
  key.replaceAll(/([\da-z])([A-Z])/gu, '$1_$2').toLowerCase();

const findPair = (map, key) =>
  map.items.find((pair) => normalizeKey(pair.key) === normalizeKey(key));

export const parseConfiguration = (text) => {
  const document = YAML.parseDocument(text ?? '', { keepCstNodes: false });

  if (document.errors?.length > 0) {
    throw new Error(
      `The configuration file contains invalid YAML: ${document.errors[0].message}`,
    );
  }

  return document;
};

// returns the plain value at the given path, or undefined if the file doesn't set it
export const getIn = (document, path) => {
  let node = document.contents;

  for (const key of path) {
    if (!(node instanceof YAMLMap)) {
      return undefined;
    }

    const pair = findPair(node, key);

    if (!pair) {
      return undefined;
    }

    node = pair.value;
  }

  if (node == null) {
    return null;
  }

  return typeof node.toJSON === 'function' ? node.toJSON() : node;
};

const createValueNode = (value) => {
  if (value === null) {
    return new Scalar(null);
  }

  const node = YAML.createNode(value, true);

  if (typeof value === 'string' && value.includes('\n')) {
    node.type = 'BLOCK_LITERAL';
  }

  return node;
};

export const setIn = (document, path, value) => {
  if (!(document.contents instanceof YAMLMap)) {
    // a file with nothing but comments parses with the comments trailing an
    // empty document; hoist them so they stay above the new content
    if (document.contents == null && document.comment) {
      document.commentBefore = document.comment;
      document.comment = undefined;
    }

    document.contents = new YAMLMap();
  }

  let map = document.contents;

  for (const [index, key] of path.entries()) {
    let pair = findPair(map, key);
    const isLeaf = index === path.length - 1;

    if (isLeaf) {
      const node = createValueNode(value);

      if (!pair) {
        map.items.push(new Pair(new Scalar(toYamlKey(key)), node));
      } else if (pair.value instanceof Scalar && node instanceof Scalar) {
        // update in place to keep any trailing comment on the line
        pair.value.value = node.value;
        pair.value.type = node.type;
      } else {
        pair.value = node;
      }

      return;
    }

    if (!pair) {
      pair = new Pair(new Scalar(toYamlKey(key)), new YAMLMap());
      map.items.push(pair);
    } else if (!(pair.value instanceof YAMLMap)) {
      pair.value = new YAMLMap();
    }

    map = pair.value;
  }
};

// removes the value at the given path so the application default applies,
// along with any parent maps left empty by the removal
export const deleteIn = (document, path) => {
  const maps = [];
  let map = document.contents;

  for (const key of path.slice(0, -1)) {
    if (!(map instanceof YAMLMap)) {
      return;
    }

    maps.push(map);
    map = findPair(map, key)?.value;
  }

  if (!(map instanceof YAMLMap)) {
    return;
  }

  const leafKey = path[path.length - 1];
  map.items = map.items.filter(
    (pair) => normalizeKey(pair.key) !== normalizeKey(leafKey),
  );

  for (let index = maps.length - 1; index >= 0; index--) {
    const parent = maps[index];
    const child = findPair(parent, path[index])?.value;

    if (child instanceof YAMLMap && child.items.length === 0) {
      parent.items = parent.items.filter(
        (pair) => normalizeKey(pair.key) !== normalizeKey(path[index]),
      );
    } else {
      break;
    }
  }
};

export const readConfiguration = async () =>
  parseConfiguration(await getYaml());

// fetches the current file, applies the mutation, validates the result on the
// server and saves it.  throws with the server's validation message on failure
export const updateConfiguration = async (mutate) => {
  const document = await readConfiguration();
  await mutate(document);

  const yaml = document.toString();
  const validationError = await validateYaml({ yaml });

  if (validationError) {
    throw new Error(validationError);
  }

  await updateYaml({ yaml });
  return document;
};

export const addToList = (document, path, item) => {
  const current = getIn(document, path);
  const list = Array.isArray(current) ? current : [];

  if (!list.includes(item)) {
    setIn(document, path, [...list, item]);
  }
};

// when the list becomes empty it's removed (along with emptied parents) unless
// keepEmpty is set, which matters when the parent map is meaningful on its own
export const removeFromList = (
  document,
  path,
  item,
  { keepEmpty = false } = {},
) => {
  const current = getIn(document, path);

  if (!Array.isArray(current)) {
    return;
  }

  const next = current.filter((entry) => entry !== item);

  if (next.length === 0 && !keepEmpty) {
    deleteIn(document, path);
  } else {
    setIn(document, path, next);
  }
};
