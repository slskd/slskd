import { addUserToBlacklist } from './userActions';
import { toast } from 'react-toastify';
import YAML from 'yaml';

jest.mock('react-toastify', () => ({
  toast: {
    error: jest.fn(),
    info: jest.fn(),
    success: jest.fn(),
  },
}));

describe('addUserToBlacklist', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('preserves existing weird comments and whitespace while appending a user', () => {
    const yamlText = `# top-level comment

transfers:
  groups:   # inline on groups
    blacklisted:

      # comment above members
      members:
        - 'alice'   # existing inline comment
        - "bob"

other: value  # unrelated trailing inline comment
`;

    const updated = addUserToBlacklist(yamlText, 'charlie');

    expect(typeof updated).toBe('string');
    expect(updated).toContain('# top-level comment');
    expect(updated).toContain('groups:   # inline on groups');
    expect(updated).toContain("- 'alice'   # existing inline comment");
    expect(updated).toContain(
      'other: value  # unrelated trailing inline comment',
    );
    expect(updated).toContain('- "charlie"');
  });

  it('detects duplicates for single and double quoted existing users', () => {
    const yamlText = `transfers:
  groups:
    blacklisted:
      members:
        - 'alice'
        - "bob"
`;

    const duplicateSingleQuoted = addUserToBlacklist(yamlText, 'alice');
    const duplicateDoubleQuoted = addUserToBlacklist(yamlText, 'bob');

    expect(duplicateSingleQuoted).toBe(false);
    expect(duplicateDoubleQuoted).toBe(false);
    expect(toast.info).toHaveBeenCalledWith(
      "User 'alice' is already in the blacklist.",
    );
    expect(toast.info).toHaveBeenCalledWith(
      "User 'bob' is already in the blacklist.",
    );
  });

  it('returns false when groups.blacklisted.members is missing', () => {
    const yamlText = `transfers:
  groups:
    blacklisted:
      notMembers:
        - alice
`;

    const result = addUserToBlacklist(yamlText, 'charlie');

    expect(result).toBe(false);
    expect(toast.error).toHaveBeenCalledWith(
      'Could not find transfers.groups.blacklisted.members in YAML configuration. Please add the structure manually first.',
    );
  });

  it('adds first member when members is an empty sequence', () => {
    const yamlText = `transfers:
  groups:
    blacklisted:
      members: []
`;

    const updated = addUserToBlacklist(yamlText, 'charlie');

    expect(typeof updated).toBe('string');
    expect(YAML.parse(updated)).toMatchObject({
      transfers: {
        groups: {
          blacklisted: {
            members: ['charlie'],
          },
        },
      },
    });
  });

  it('handles appending when the last list item has no terminal newline', () => {
    const yamlText = `transfers:
  groups:
    blacklisted:
      members:
        - alice`;

    const updated = addUserToBlacklist(yamlText, 'bob');

    expect(typeof updated).toBe('string');
    expect(YAML.parse(updated)).toMatchObject({
      transfers: {
        groups: {
          blacklisted: {
            members: ['alice', 'bob'],
          },
        },
      },
    });
    expect(updated).toContain('alice\n');
  });

  it('safely serializes plausible usernames with special characters', () => {
    const yamlText = `transfers:
  groups:
    blacklisted:
      members:
        - "existing user"
`;

    const weirdNames = [
      'John Doe',
      'mr.robot',
      'dj_shadow',
      'alice-01',
      "O'Brien",
      'sam\\west',
      'name: with # symbols',
    ];

    let updated = yamlText;
    for (const name of weirdNames) {
      updated = addUserToBlacklist(updated, name);
      expect(typeof updated).toBe('string');
    }

    expect(YAML.parse(updated)).toMatchObject({
      transfers: {
        groups: {
          blacklisted: {
            members: ['existing user', ...weirdNames],
          },
        },
      },
    });
  });

  it('detects duplicates for special usernames regardless of quote style', () => {
    const yamlText = `transfers:
  groups:
    blacklisted:
      members:
        - "name: with # symbols"
        - 'John Doe'
        - "sam\\\\west"
`;

    expect(addUserToBlacklist(yamlText, 'name: with # symbols')).toBe(false);
    expect(addUserToBlacklist(yamlText, 'John Doe')).toBe(false);
    expect(addUserToBlacklist(yamlText, 'sam\\west')).toBe(false);
    expect(toast.info).toHaveBeenCalledWith(
      "User 'name: with # symbols' is already in the blacklist.",
    );
    expect(toast.info).toHaveBeenCalledWith(
      "User 'John Doe' is already in the blacklist.",
    );
    expect(toast.info).toHaveBeenCalledWith(
      "User 'sam\\west' is already in the blacklist.",
    );
  });

  it('keeps a realistic blacklist config parseable after appending a user', () => {
    const yamlText = `soulseek:
  username: "sksksksksksk"
  description: "example profile"
transfers:
  groups:
    blacklisted:
      members:
        - TedKaczynski
        - ioki
        - LoS
        - box cat
        - "Sgt Floofy's Mac Account"
        - "$!*COMMANC7HE!"
        - "DJ H4UNTED TR4PHOUSE"
        - \\your mom and police
        - "/ / /   +p  u  r  e   /"
        - "!>NinjaK11B-X 0"
`;

    const updated = addUserToBlacklist(yamlText, 'new person 42');

    expect(typeof updated).toBe('string');
    expect(YAML.parse(updated)).toMatchObject({
      transfers: {
        groups: {
          blacklisted: {
            members: [
              'TedKaczynski',
              'ioki',
              'LoS',
              'box cat',
              "Sgt Floofy's Mac Account",
              '$!*COMMANC7HE!',
              'DJ H4UNTED TR4PHOUSE',
              '\\your mom and police',
              '/ / /   +p  u  r  e   /',
              '!>NinjaK11B-X 0',
              'new person 42',
            ],
          },
        },
      },
    });
  });
});
