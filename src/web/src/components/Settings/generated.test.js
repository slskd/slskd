import {
  buildCategories,
  entryFields,
  fieldFromNode,
  humanize,
} from './generated';
import { unlimitedValue } from './schema';

describe('humanize', () => {
  it.each([
    ['listenPort', 'Listen port'],
    ['listenIpAddress', 'Listen IP address'],
    ['logSQL', 'Log SQL'],
    ['apiKeys', 'API keys'],
    ['url', 'URL'],
    ['firstinfirstout', 'Firstinfirstout'],
    ['noConnect', 'No connect'],
  ])('%s -> %s', (name, expected) => {
    expect(humanize(name)).toBe(expected);
  });
});

describe('fieldFromNode', () => {
  it('maps booleans to toggles and describes the default', () => {
    const field = fieldFromNode(
      {
        default: false,
        description: 'disable the distributed network',
        name: 'disabled',
        type: 'boolean',
      },
      ['soulseek', 'distributedNetwork', 'disabled'],
    );

    expect(field).toMatchObject({
      help: 'Disable the distributed network. Default: off.',
      key: 'soulseek.distributedNetwork.disabled',
      label: 'Disabled',
      type: 'toggle',
    });
  });

  it('treats int.MaxValue defaults as unlimited and hides them', () => {
    const field = fieldFromNode(
      {
        default: unlimitedValue,
        maximum: unlimitedValue,
        minimum: 1,
        name: 'speedLimit',
        type: 'integer',
      },
      ['transfers', 'upload', 'speedLimit'],
    );

    expect(field).toMatchObject({ min: 1, type: 'number', unlimited: true });
    expect(field.max).toBeUndefined();
    expect(field.help).toBe('');
  });

  it('maps restricted strings to selects, with a blank choice when there is no default', () => {
    expect(
      fieldFromNode(
        {
          default: 'memory',
          name: 'storageMode',
          type: 'string',
          values: ['memory', 'disk'],
        },
        ['shares', 'cache', 'storageMode'],
      ).options,
    ).toEqual([
      ['memory', 'Memory'],
      ['disk', 'Disk'],
    ]);

    expect(
      fieldFromNode({ name: 'role', type: 'string', values: ['readonly'] }, [
        'role',
      ]).options[0],
    ).toEqual(['', 'Not set']);
  });

  it('maps secrets to passwords and never describes their default', () => {
    expect(
      fieldFromNode(
        { default: 'hunter2', name: 'password', secret: true, type: 'string' },
        ['soulseek', 'password'],
      ),
    ).toMatchObject({ help: '', secret: true, type: 'password' });
  });

  it('maps string arrays to lists, and restricted ones to multiselects', () => {
    expect(
      fieldFromNode({ name: 'rooms', type: 'array' }, ['rooms']).type,
    ).toBe('list');
    expect(
      fieldFromNode(
        { name: 'on', type: 'array', values: ['downloadfilecomplete'] },
        ['on'],
      ).type,
    ).toBe('multiselect');
  });

  it('maps arrays of flat objects to row editors', () => {
    const field = fieldFromNode(
      {
        items: {
          properties: [
            { name: 'name', type: 'string' },
            { name: 'value', type: 'string' },
          ],
          type: 'object',
        },
        name: 'headers',
        type: 'array',
      },
      ['headers'],
    );

    expect(field.type).toBe('objectList');
    expect(field.columns.map((column) => column.name)).toEqual([
      'name',
      'value',
    ]);
  });
});

const root = {
  properties: [
    { default: false, name: 'debug', type: 'boolean' },
    {
      name: 'soulseek',
      properties: [
        { name: 'username', type: 'string' },
        {
          name: 'connection',
          properties: [
            {
              name: 'timeout',
              properties: [
                { default: 10_000, name: 'connect', type: 'integer' },
              ],
              type: 'object',
            },
          ],
          type: 'object',
        },
      ],
      type: 'object',
    },
    {
      name: 'integrations',
      properties: [
        {
          items: {
            properties: [
              { name: 'on', type: 'array', values: ['a', 'b'] },
              {
                name: 'call',
                properties: [{ name: 'url', type: 'string' }],
                type: 'object',
              },
            ],
            type: 'object',
          },
          name: 'webhooks',
          type: 'dictionary',
        },
      ],
      type: 'object',
    },
  ],
  type: 'object',
};

describe('buildCategories', () => {
  it('puts top level values in Application and nests objects as titled groups', () => {
    const categories = buildCategories(root);

    expect(categories.map((category) => category.title)).toEqual([
      'Application',
      'Soulseek',
      'Integrations',
    ]);

    const soulseek = categories[1];
    expect(soulseek.groups.map((group) => group.title)).toEqual([
      'Soulseek',
      'Soulseek › Connection › Timeout',
    ]);
    expect(soulseek.groups[1].fields[0].segments).toEqual([
      'soulseek',
      'connection',
      'timeout',
      'connect',
    ]);
  });

  it('turns dictionaries into entry editors', () => {
    const integrations = buildCategories(root)[2];
    const [dictionary] = integrations.groups[0].dictionaries;

    expect(dictionary.segments).toEqual(['integrations', 'webhooks']);
    expect(
      entryFields(dictionary.itemSchema, [
        ...dictionary.segments,
        'notify',
      ]).map((field) => [field.label, field.segments.join('.'), field.type]),
    ).toEqual([
      ['On', 'integrations.webhooks.notify.on', 'multiselect'],
      ['Call › URL', 'integrations.webhooks.notify.call.url', 'text'],
    ]);
  });
});
