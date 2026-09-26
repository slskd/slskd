import {
  addToList,
  deleteIn,
  getIn,
  parseConfiguration,
  removeFromList,
  setIn,
  toYamlKey,
} from './configuration';

describe('toYamlKey', () => {
  it('converts camelCase to snake_case', () => {
    expect(toYamlKey('listenPort')).toBe('listen_port');
    expect(toYamlKey('caseSensitiveRegEx')).toBe('case_sensitive_reg_ex');
    expect(toYamlKey('port')).toBe('port');
  });
});

describe('getIn', () => {
  it('matches keys regardless of case and underscores', () => {
    const document = parseConfiguration('soulseek:\n  listenPort: 1234\n');
    expect(getIn(document, ['soulseek', 'listen_port'])).toBe(1_234);
    expect(getIn(document, ['Soulseek', 'listenport'])).toBe(1_234);
  });

  it('returns undefined for missing keys', () => {
    const document = parseConfiguration('web:\n  port: 5030\n');
    expect(getIn(document, ['soulseek', 'username'])).toBeUndefined();
    expect(getIn(document, ['web', 'port', 'nope'])).toBeUndefined();
  });

  it('returns plain values for collections', () => {
    const document = parseConfiguration('shares:\n  directories:\n    - a\n');
    expect(getIn(document, ['shares', 'directories'])).toEqual(['a']);
  });
});

describe('setIn', () => {
  it('preserves comments and unrelated keys', () => {
    const document = parseConfiguration(
      '# my config\nweb:\n  port: 5030 # the port\n  url_base: /\n',
    );

    setIn(document, ['web', 'port'], 6_000);

    const text = document.toString();
    expect(text).toContain('# my config');
    expect(text).toContain('port: 6000 # the port');
    expect(text).toContain('url_base: /');
  });

  it('reuses the existing spelling of a key', () => {
    const document = parseConfiguration('soulseek:\n  listenPort: 1\n');
    setIn(document, ['soulseek', 'listenPort'], 2);
    expect(document.toString()).toBe('soulseek:\n  listenPort: 2\n');
  });

  it('creates missing maps using snake_case keys', () => {
    const document = parseConfiguration('web:\n  port: 5030\n');
    setIn(document, ['soulseek', 'listenPort'], 50_300);
    expect(getIn(document, ['soulseek', 'listen_port'])).toBe(50_300);
    expect(document.toString()).toContain('listen_port: 50300');
  });

  it('keeps comments above content when the file was only comments', () => {
    const document = parseConfiguration('# debug: false\n# web:\n');
    setIn(document, ['debug'], true);

    const text = document.toString();
    expect(text.indexOf('# debug: false')).toBeLessThan(
      text.indexOf('debug: true'),
    );
  });

  it('works on an empty file', () => {
    const document = parseConfiguration('');
    setIn(document, ['soulseek', 'description'], 'hi');
    expect(getIn(document, ['soulseek', 'description'])).toBe('hi');
  });

  it('writes multi-line strings as block literals', () => {
    const document = parseConfiguration('soulseek:\n  description: old\n');
    setIn(document, ['soulseek', 'description'], 'line one\nline two\n');

    const text = document.toString();
    expect(text).toContain('description: |');
    expect(getIn(parseConfiguration(text), ['soulseek', 'description'])).toBe(
      'line one\nline two\n',
    );
  });

  it('replaces a scalar with a list', () => {
    const document = parseConfiguration('rooms: ~\n');
    setIn(document, ['rooms'], ['a', 'b']);
    expect(getIn(document, ['rooms'])).toEqual(['a', 'b']);
  });
});

describe('deleteIn', () => {
  it('removes the key and prunes emptied parents', () => {
    const document = parseConfiguration(
      'web:\n  port: 1\nsoulseek:\n  connection:\n    proxy:\n      enabled: true\n',
    );

    deleteIn(document, ['soulseek', 'connection', 'proxy', 'enabled']);

    expect(document.toString()).toBe('web:\n  port: 1\n');
  });

  it('keeps parents that still have content', () => {
    const document = parseConfiguration('web:\n  port: 1\n  url_base: /\n');
    deleteIn(document, ['web', 'port']);
    expect(document.toString()).toBe('web:\n  url_base: /\n');
  });

  it('ignores missing paths', () => {
    const document = parseConfiguration('web:\n  port: 1\n');
    deleteIn(document, ['soulseek', 'username']);
    deleteIn(document, ['web', 'port', 'deeper']);
    expect(document.toString()).toBe('web:\n  port: 1\n');
  });
});

describe('lists', () => {
  const path = ['transfers', 'groups', 'blacklisted', 'members'];

  it('adds without duplicating', () => {
    const document = parseConfiguration('');
    addToList(document, path, 'alice');
    addToList(document, path, 'alice');
    addToList(document, path, 'bob');
    expect(getIn(document, path)).toEqual(['alice', 'bob']);
  });

  it('removes and cleans up when empty', () => {
    const document = parseConfiguration(
      'transfers:\n  groups:\n    blacklisted:\n      members:\n        - alice\n',
    );
    removeFromList(document, path, 'alice');
    // an empty map, rather than an empty document, so the server always sees a mapping
    expect(document.toString().trim()).toBe('{}');
  });

  it('can keep an emptied list so its parent survives', () => {
    const groupPath = ['transfers', 'groups', 'userDefined', 'pals', 'members'];
    const document = parseConfiguration(
      'transfers:\n  groups:\n    user_defined:\n      pals:\n        members:\n          - alice\n',
    );
    removeFromList(document, groupPath, 'alice', { keepEmpty: true });
    expect(getIn(document, groupPath)).toEqual([]);
  });
});

describe('parseConfiguration', () => {
  it('throws on invalid YAML', () => {
    expect(() => parseConfiguration('a: [1, 2\n')).toThrow(/invalid YAML/u);
  });
});
