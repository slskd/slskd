// builds settings fields from the schema the server derives from its options
// class, so every option can be edited even if the curated sections above
// don't cover it
import { unlimitedValue } from './schema';

const acronyms = {
  api: 'API',
  cidr: 'CIDR',
  cidrs: 'CIDRs',
  ftp: 'FTP',
  http: 'HTTP',
  https: 'HTTPS',
  id: 'ID',
  ip: 'IP',
  jwt: 'JWT',
  pfx: 'PFX',
  regex: 'RegEx',
  sql: 'SQL',
  ttl: 'TTL',
  url: 'URL',
  vpn: 'VPN',
};

// 'listenIpAddress' -> 'Listen IP address', 'logSQL' -> 'Log SQL'
export const humanize = (name) => {
  const words = String(name)
    .replaceAll(/([a-z\d])([A-Z])/gu, '$1 $2')
    .replaceAll(/([A-Z]+)([A-Z][a-z])/gu, '$1 $2')
    .split(/[\s_-]+/u)
    .filter(Boolean)
    .map((word) => acronyms[word.toLowerCase()] ?? word.toLowerCase());

  if (words.length === 0) {
    return '';
  }

  const [first, ...rest] = words;
  return [first.charAt(0).toUpperCase() + first.slice(1), ...rest].join(' ');
};

const sentence = (text) => {
  if (!text) {
    return undefined;
  }

  const trimmed = text.trim();
  const capitalized = trimmed.charAt(0).toUpperCase() + trimmed.slice(1);
  return /[.!?]$/u.test(capitalized) ? capitalized : `${capitalized}.`;
};

const describeDefault = (node) => {
  const value = node.default;

  if (value == null || node.secret || node.path) {
    return undefined;
  }

  if (node.type === 'boolean') {
    return `Default: ${value ? 'on' : 'off'}.`;
  }

  if (node.type === 'integer' || node.type === 'number') {
    if (value === unlimitedValue) {
      return undefined;
    }

    // ports and versions read wrong with thousands separators
    return /port$|version$/iu.test(node.name)
      ? `Default: ${value}.`
      : `Default: ${Number(value).toLocaleString()}.`;
  }

  if (node.type === 'array') {
    return value.length > 0 ? `Default: ${value.join(', ')}.` : undefined;
  }

  // quoted, since text defaults can end in punctuation of their own
  return value === '' ? undefined : `Default: “${value}”.`;
};

const isLeaf = (node) =>
  !['object', 'dictionary'].includes(node.type) &&
  !(node.type === 'array' && node.items);

// arrays of small objects, like a webhook's http headers, are edited as rows
const isObjectList = (node) =>
  node.type === 'array' &&
  node.items?.properties?.length > 0 &&
  node.items.properties.every(isLeaf);

export const fieldFromNode = (node, segments, { label } = {}) => {
  const base = {
    help: [sentence(node.description), describeDefault(node)]
      .filter(Boolean)
      .join(' '),
    isPath: node.path,
    key: segments.join('.'),
    label: label ?? humanize(node.name),
    reconnect: node.requiresReconnect,
    restart: node.requiresRestart,
    schemaDefault: node.default,
    secret: node.secret,
    segments,
  };

  if (isObjectList(node)) {
    return {
      ...base,
      columns: node.items.properties.map((property) => ({
        label: humanize(property.name),
        name: property.name,
      })),
      type: 'objectList',
    };
  }

  switch (node.type) {
    case 'boolean':
      return { ...base, type: 'toggle' };
    case 'integer':
    case 'number':
      return {
        ...base,
        max: node.maximum === unlimitedValue ? undefined : node.maximum,
        min: node.minimum,
        type: 'number',
        unlimited: node.default === unlimitedValue,
      };
    case 'array':
      return node.values
        ? {
            ...base,
            options: node.values.map((value) => [value, humanize(value)]),
            type: 'multiselect',
          }
        : { ...base, type: 'list' };
    case 'string':
      if (node.secret) {
        return { ...base, type: 'password' };
      }

      if (node.values) {
        return {
          ...base,
          options: [
            ...(node.default == null ? [['', 'Not set']] : []),
            ...node.values.map((value) => [value, humanize(value)]),
          ],
          type: 'select',
        };
      }

      // free text shown to other people, like the profile description
      return node.name === 'description'
        ? { ...base, type: 'textarea' }
        : { ...base, type: 'text' };
    default:
      return undefined;
  }
};

// the fields of one dictionary entry (a webhook, a script, ...), with nested
// objects flattened into the list and labelled with their path
export const entryFields = (itemSchema, entrySegments) => {
  const fields = [];

  const walk = (node, segments, labels) => {
    for (const child of node.properties ?? []) {
      const childSegments = [...segments, child.name];
      const childLabels = [...labels, humanize(child.name)];

      if (child.type === 'object') {
        walk(child, childSegments, childLabels);
      } else {
        const field = fieldFromNode(child, childSegments, {
          label: childLabels.join(' › '),
        });

        if (field) {
          fields.push(field);
        }
      }
    }
  };

  walk(itemSchema, entrySegments, []);
  return fields;
};

// turns the schema into categories (one per top level option) holding groups
// of fields; nested objects become their own groups, titled with their path.
// dictionaries become editors for named entries
export const buildCategories = (root) => {
  const application = {
    groups: [{ fields: [], key: 'application', title: 'Application' }],
    key: 'application',
    title: 'Application',
  };
  const categories = [application];

  const collect = (node, segments, titles, category) => {
    const group = {
      dictionaries: [],
      fields: [],
      key: segments.join('.'),
      title: titles.join(' › '),
    };
    const children = [];

    for (const child of node.properties ?? []) {
      const childSegments = [...segments, child.name];

      if (child.type === 'object') {
        children.push(child);
      } else if (child.type === 'dictionary') {
        group.dictionaries.push({
          help: sentence(child.description),
          itemSchema: child.items,
          key: childSegments.join('.'),
          segments: childSegments,
          title: humanize(child.name),
        });
      } else {
        const field = fieldFromNode(child, childSegments);

        if (field) {
          group.fields.push(field);
        }
      }
    }

    if (group.fields.length > 0 || group.dictionaries.length > 0) {
      category.groups.push(group);
    }

    for (const child of children) {
      collect(
        child,
        [...segments, child.name],
        [...titles, humanize(child.name)],
        category,
      );
    }
  };

  for (const node of root?.properties ?? []) {
    if (node.type === 'object') {
      const category = {
        groups: [],
        key: node.name,
        title: humanize(node.name),
      };
      collect(node, [node.name], [humanize(node.name)], category);
      categories.push(category);
    } else {
      const field = fieldFromNode(node, [node.name]);

      if (field) {
        application.groups[0].fields.push(field);
      }
    }
  }

  return categories.filter((category) =>
    category.groups.some(
      (group) => group.fields.length > 0 || group.dictionaries?.length > 0,
    ),
  );
};
