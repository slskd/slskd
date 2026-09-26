// the settings shown in the settings page, grouped like nicotine+'s
// preferences.  keys are dotted paths into the options object as the api
// returns it (camelCase); they're written to the config file in snake_case.
//
// field properties:
//   key        dotted path to the option
//   label      short name
//   help       one line explaining what it does, shown under the field
//   type       text | password | number | toggle | select | list | textarea
//   unit       shown after number fields
//   min, max   bounds for number fields
//   options    choices for select fields, [value, text]
//   unlimited  number fields whose default is 'no limit' (int.MaxValue);
//              shown empty, and emptying them restores the default
//   empty      placeholder describing what an empty field means
//   restart    change takes effect after restarting the application
//   reconnect  change takes effect after reconnecting to the server
//   isPath     the value is a filesystem path, which the server may rewrite;
//              don't flag differences from the file as overrides
//   secret     value is redacted by the api and never displayed

const minutes = 'minutes';
const kib = 'KiB/s';

const limitFields = (prefix, label) => [
  {
    empty: 'No limit',
    key: `${prefix}.files`,
    label: `${label} files`,
    min: 1,
    type: 'number',
  },
  {
    empty: 'No limit',
    key: `${prefix}.megabytes`,
    label: `${label} size`,
    min: 1,
    type: 'number',
    unit: 'MB',
  },
];

const failureField = (prefix, label) => ({
  empty: 'No limit',
  help: 'Stop accepting requests from a user after this many failed uploads.',
  key: `${prefix}.failures`,
  label: `${label} failures`,
  min: 1,
  type: 'number',
});

const strategyOptions = [
  ['roundrobin', 'Round robin (take turns between users)'],
  ['firstinfirstout', 'First in, first out'],
];

const retentionField = (key, label) => ({
  empty: 'Keep forever',
  key,
  label,
  min: 5,
  type: 'number',
  unit: minutes,
});

const schema = [
  {
    description:
      'The picture and description other users see when they view your profile.',
    icon: 'id card',
    key: 'profile',
    title: 'Profile',
  },
  {
    description: 'Your Soulseek account and how other users connect to you.',
    groups: [
      {
        fields: [
          {
            help: 'Changing this logs in as a different account.',
            key: 'soulseek.username',
            label: 'Username',
            reconnect: true,
            type: 'text',
          },
          {
            key: 'soulseek.password',
            label: 'Password',
            reconnect: true,
            secret: true,
            type: 'password',
          },
        ],
        title: 'Account',
      },
      {
        fields: [
          {
            help: 'Other users connect to you on this port. Forward it on your router so they can reach you.',
            key: 'soulseek.listenPort',
            label: 'Listening port',
            max: 65_535,
            min: 1_024,
            type: 'number',
          },
          {
            help: '0.0.0.0 listens on every network interface.',
            key: 'soulseek.listenIpAddress',
            label: 'Listening address',
            type: 'text',
          },
        ],
        title: 'Incoming connections',
      },
      {
        fields: [
          {
            help: 'The distributed network relays search requests between users.',
            key: 'soulseek.distributedNetwork.disabled',
            label: 'Disable the distributed network',
            type: 'toggle',
          },
          {
            key: 'soulseek.distributedNetwork.disableChildren',
            label: 'Don’t accept child connections',
            type: 'toggle',
          },
          {
            key: 'soulseek.distributedNetwork.childLimit',
            label: 'Maximum children',
            min: 1,
            type: 'number',
          },
        ],
        title: 'Distributed network',
      },
      {
        advanced: true,
        fields: [
          {
            key: 'soulseek.connection.proxy.enabled',
            label: 'Use a SOCKS5 proxy',
            type: 'toggle',
          },
          {
            key: 'soulseek.connection.proxy.address',
            label: 'Proxy address',
            type: 'text',
          },
          {
            key: 'soulseek.connection.proxy.port',
            label: 'Proxy port',
            max: 65_535,
            min: 1,
            type: 'number',
          },
          {
            key: 'soulseek.connection.proxy.username',
            label: 'Proxy username',
            type: 'text',
          },
          {
            key: 'soulseek.connection.proxy.password',
            label: 'Proxy password',
            secret: true,
            type: 'password',
          },
        ],
        title: 'Proxy',
      },
      {
        advanced: true,
        fields: [
          {
            key: 'soulseek.address',
            label: 'Server address',
            reconnect: true,
            type: 'text',
          },
          {
            key: 'soulseek.port',
            label: 'Server port',
            max: 65_535,
            min: 1_024,
            reconnect: true,
            type: 'number',
          },
          {
            key: 'soulseek.connection.timeout.connect',
            label: 'Connect timeout',
            min: 1_000,
            type: 'number',
            unit: 'ms',
          },
          {
            key: 'soulseek.connection.timeout.inactivity',
            label: 'Inactivity timeout',
            min: 1_000,
            type: 'number',
            unit: 'ms',
          },
          {
            key: 'soulseek.connection.timeout.transfer',
            label: 'Transfer timeout',
            min: 30_000,
            type: 'number',
            unit: 'ms',
          },
          {
            key: 'soulseek.diagnosticLevel',
            label: 'Diagnostic log level',
            options: [
              ['none', 'None'],
              ['warning', 'Warning'],
              ['info', 'Info'],
              ['debug', 'Debug'],
              ['trace', 'Trace'],
            ],
            restart: true,
            type: 'select',
          },
        ],
        title: 'Server and timeouts',
      },
    ],
    icon: 'plug',
    key: 'network',
    title: 'Network',
  },
  {
    description: 'The folders other users can browse and download from.',
    groups: [
      {
        fields: [
          {
            help: 'One folder per line, as paths on the server. Prefix with [Alias] to rename a share, or with ! to exclude a subfolder.',
            key: 'shares.directories',
            label: 'Shared folders',
            isPath: true,
            placeholder: '/music or [Music]/data/music or !/music/private',
            type: 'list',
          },
          {
            help: 'Files whose path matches any of these regular expressions aren’t shared.',
            key: 'shares.filters',
            label: 'Excluded files',
            placeholder: '\\.ini$',
            type: 'list',
          },
        ],
        title: 'Shares',
      },
      {
        fields: [
          {
            empty: 'Never',
            help: 'Rescan shares automatically after this long.',
            key: 'shares.cache.retention',
            label: 'Rescan every',
            min: 60,
            type: 'number',
            unit: minutes,
          },
          {
            help: 'Disk storage uses less memory for very large shares.',
            key: 'shares.cache.storageMode',
            label: 'Share index storage',
            options: [
              ['memory', 'Memory'],
              ['disk', 'Disk'],
            ],
            restart: true,
            type: 'select',
          },
          {
            key: 'shares.cache.workers',
            label: 'Scan workers',
            max: 128,
            min: 1,
            restart: true,
            type: 'number',
          },
        ],
        title: 'Scanning',
      },
    ],
    icon: 'share alternate',
    key: 'shares',
    link: {
      text: 'Rescan and inspect shares in System › Shares',
      to: '/system/shares',
    },
    title: 'Shares',
  },
  {
    description: 'Where downloads go and how fast they run.',
    groups: [
      {
        fields: [
          {
            key: 'directories.downloads',
            label: 'Download folder',
            isPath: true,
            restart: true,
            type: 'text',
          },
          {
            key: 'directories.incomplete',
            label: 'Incomplete download folder',
            isPath: true,
            restart: true,
            type: 'text',
          },
          {
            // these are literal tokens for the server, not template placeholders
            // eslint-disable-next-line no-template-curly-in-string
            help: 'Folder created inside the download folder for each download. Tokens: ${SOURCE_USERNAME}, ${SOURCE_PATH}, ${SOURCE_DIRECTORY}, ${BATCH_ID}, ${SEARCH_TEXT}.',
            key: 'transfers.download.destination.subdirectory',
            label: 'Subfolder',
            type: 'text',
          },
          {
            key: 'transfers.download.destination.exists',
            label: 'If the file already exists',
            options: [
              ['rename', 'Keep both (rename the new file)'],
              ['overwrite', 'Overwrite'],
            ],
            type: 'select',
          },
          {
            help: 'chmod style, for example 644. Has no effect on Windows.',
            key: 'transfers.download.destination.permissions.mode',
            label: 'File permissions',
            placeholder: '644',
            type: 'text',
          },
        ],
        title: 'Folders',
      },
      {
        fields: [
          {
            key: 'transfers.download.slots',
            label: 'Simultaneous downloads',
            min: 1,
            restart: true,
            type: 'number',
            unlimited: true,
          },
          {
            key: 'transfers.download.speedLimit',
            label: 'Speed limit',
            min: 1,
            type: 'number',
            unit: kib,
            unlimited: true,
          },
        ],
        title: 'Limits',
      },
      {
        fields: [
          {
            key: 'transfers.download.retry.attempts',
            label: 'Attempts',
            min: 1,
            type: 'number',
          },
          {
            key: 'transfers.download.retry.delay',
            label: 'First retry after',
            min: 1_000,
            type: 'number',
            unit: 'ms',
          },
          {
            key: 'transfers.download.retry.maxDelay',
            label: 'Longest wait between retries',
            min: 30_000,
            type: 'number',
            unit: 'ms',
          },
          {
            key: 'transfers.download.retry.partial',
            label: 'Partially downloaded files',
            options: [
              ['resume', 'Resume'],
              ['overwrite', 'Start over'],
            ],
            type: 'select',
          },
        ],
        title: 'Retries',
      },
    ],
    icon: 'download',
    key: 'downloads',
    title: 'Downloads',
  },
  {
    description: 'How many people can download from you, and how fast.',
    groups: [
      {
        fields: [
          {
            key: 'transfers.upload.slots',
            label: 'Upload slots',
            min: 1,
            restart: true,
            type: 'number',
          },
          {
            key: 'transfers.upload.speedLimit',
            label: 'Speed limit',
            min: 1,
            type: 'number',
            unit: kib,
            unlimited: true,
          },
        ],
        title: 'Everyone',
      },
      {
        fields: [
          {
            key: 'transfers.groups.default.upload.slots',
            label: 'Upload slots',
            min: 1,
            type: 'number',
            unlimited: true,
          },
          {
            key: 'transfers.groups.default.upload.speedLimit',
            label: 'Speed limit',
            min: 1,
            type: 'number',
            unit: kib,
            unlimited: true,
          },
          {
            key: 'transfers.groups.default.upload.strategy',
            label: 'Queue order',
            options: strategyOptions,
            type: 'select',
          },
          {
            help: 'Lower numbers are served first.',
            key: 'transfers.groups.default.upload.priority',
            label: 'Priority',
            min: 1,
            type: 'number',
          },
        ],
        title: 'Regular users',
      },
      {
        fields: [
          {
            help: 'Users sharing fewer files than this are treated as leechers.',
            key: 'transfers.groups.leechers.thresholds.files',
            label: 'Minimum shared files',
            min: 1,
            type: 'number',
          },
          {
            key: 'transfers.groups.leechers.thresholds.directories',
            label: 'Minimum shared folders',
            min: 1,
            type: 'number',
          },
          {
            key: 'transfers.groups.leechers.upload.slots',
            label: 'Upload slots',
            min: 1,
            type: 'number',
            unlimited: true,
          },
          {
            key: 'transfers.groups.leechers.upload.speedLimit',
            label: 'Speed limit',
            min: 1,
            type: 'number',
            unit: kib,
            unlimited: true,
          },
          {
            key: 'transfers.groups.leechers.upload.priority',
            label: 'Priority',
            min: 1,
            type: 'number',
          },
        ],
        title: 'Leechers',
      },
      {
        advanced: true,
        fields: [
          ...limitFields('transfers.upload.limits.queued', 'Queued'),
          ...limitFields('transfers.upload.limits.daily', 'Daily'),
          failureField('transfers.upload.limits.daily', 'Daily'),
          ...limitFields('transfers.upload.limits.weekly', 'Weekly'),
          failureField('transfers.upload.limits.weekly', 'Weekly'),
        ],
        title: 'Per-user limits',
      },
    ],
    icon: 'upload',
    key: 'uploads',
    title: 'Uploads',
  },
  {
    description:
      'Banned users, and groups of users with their own upload rules.',
    groups: [
      {
        fields: [
          {
            help: 'Banned users can’t download from you or browse your shares, and their search results are hidden.',
            key: 'transfers.groups.blacklisted.members',
            label: 'Banned users',
            placeholder: 'username',
            type: 'list',
          },
          {
            help: 'Regular expressions matched against usernames.',
            key: 'transfers.groups.blacklisted.patterns',
            label: 'Banned username patterns',
            type: 'list',
          },
          {
            key: 'transfers.groups.blacklisted.cidrs',
            label: 'Banned IP ranges',
            placeholder: '203.0.113.0/24',
            type: 'list',
          },
        ],
        title: 'Ban list',
      },
      {
        fields: [
          {
            key: 'blacklist.enabled',
            label: 'Use an IP blocklist file',
            restart: true,
            type: 'toggle',
          },
          {
            help: 'A file of CIDRs, in P2P, DAT or CIDR list format.',
            key: 'blacklist.file',
            label: 'Blocklist file',
            isPath: true,
            type: 'text',
          },
        ],
        title: 'Blocklist',
      },
    ],
    icon: 'users',
    key: 'users',
    title: 'Users & Bans',
    userGroups: true,
  },
  {
    description:
      'Chat rooms, and how your shares answer other people’s searches.',
    groups: [
      {
        fields: [
          {
            key: 'rooms',
            label: 'Join these rooms at startup',
            placeholder: 'room name',
            type: 'list',
          },
        ],
        title: 'Rooms',
      },
      {
        fields: [
          {
            help: 'Incoming searches matching any of these regular expressions are ignored.',
            key: 'filters.search.request',
            label: 'Ignored searches',
            placeholder: '^.{1,2}$',
            type: 'list',
          },
          {
            key: 'throttling.search.incoming.responseFileLimit',
            label: 'Most files per search response',
            max: 5_000,
            min: 100,
            type: 'number',
          },
          {
            key: 'throttling.search.incoming.concurrency',
            label: 'Searches answered at once',
            max: 100,
            min: 1,
            restart: true,
            type: 'number',
          },
        ],
        title: 'Incoming searches',
      },
      {
        fields: [retentionField('retention.search', 'Keep your searches for')],
        title: 'Your searches',
      },
    ],
    icon: 'search',
    key: 'search',
    title: 'Searches & Rooms',
  },
  {
    description: 'Access to this web interface.',
    groups: [
      {
        fields: [
          {
            help: 'Anyone who can reach the web UI can use it. Only disable this on a trusted network.',
            key: 'web.authentication.disabled',
            label: 'Disable login',
            restart: true,
            type: 'toggle',
          },
          {
            key: 'web.authentication.username',
            label: 'Username',
            type: 'text',
          },
          {
            key: 'web.authentication.password',
            label: 'Password',
            secret: true,
            type: 'password',
          },
          {
            help: 'How long a login lasts.',
            key: 'web.authentication.jwt.ttl',
            label: 'Session length',
            min: 3_600,
            restart: true,
            type: 'number',
            unit: 'ms',
          },
        ],
        title: 'Login',
      },
      {
        fields: [
          {
            key: 'web.port',
            label: 'HTTP port',
            max: 65_535,
            min: 1,
            restart: true,
            type: 'number',
          },
          {
            help: 'For serving the UI under a subpath behind a reverse proxy, like /slskd.',
            key: 'web.urlBase',
            label: 'URL base',
            restart: true,
            type: 'text',
          },
          {
            key: 'web.https.disabled',
            label: 'Disable HTTPS',
            restart: true,
            type: 'toggle',
          },
          {
            key: 'web.https.port',
            label: 'HTTPS port',
            max: 65_535,
            min: 1,
            restart: true,
            type: 'number',
          },
          {
            key: 'web.https.force',
            label: 'Redirect HTTP to HTTPS',
            restart: true,
            type: 'toggle',
          },
        ],
        title: 'Web server',
      },
      {
        fields: [
          {
            help: 'Turning this off also turns off this settings page; you’ll have to edit the config file to turn it back on.',
            key: 'remoteConfiguration',
            label: 'Allow changing settings from the web UI',
            type: 'toggle',
          },
          {
            help: 'Lets the web UI delete files in the download folders.',
            key: 'remoteFileManagement',
            label: 'Allow managing files from the web UI',
            type: 'toggle',
          },
          {
            key: 'instanceName',
            label: 'Instance name',
            restart: true,
            type: 'text',
          },
        ],
        title: 'Application',
      },
    ],
    icon: 'lock',
    key: 'web',
    title: 'Web & Security',
  },
  {
    description: 'How long finished transfers, files and logs are kept.',
    groups: [
      {
        fields: [
          retentionField(
            'retention.transfers.download.succeeded',
            'Finished downloads',
          ),
          retentionField(
            'retention.transfers.download.errored',
            'Errored downloads',
          ),
          retentionField(
            'retention.transfers.download.cancelled',
            'Cancelled downloads',
          ),
          retentionField(
            'retention.transfers.download.failed',
            'Failed downloads',
          ),
          retentionField(
            'retention.transfers.upload.succeeded',
            'Finished uploads',
          ),
          retentionField(
            'retention.transfers.upload.errored',
            'Errored uploads',
          ),
          retentionField(
            'retention.transfers.upload.cancelled',
            'Cancelled uploads',
          ),
          retentionField('retention.transfers.upload.failed', 'Failed uploads'),
        ],
        help: 'Removes transfers from the lists; downloaded files aren’t touched.',
        title: 'Transfer lists',
      },
      {
        fields: [
          {
            ...retentionField('retention.files.complete', 'Downloaded files'),
            help: 'Deletes downloaded files that haven’t been opened for this long.',
            min: 30,
          },
          {
            ...retentionField('retention.files.incomplete', 'Incomplete files'),
            min: 30,
          },
        ],
        title: 'Files',
      },
      {
        fields: [
          {
            key: 'retention.logs',
            label: 'Keep logs for',
            min: 7,
            type: 'number',
            unit: 'days',
          },
          {
            key: 'logger.noDisk',
            label: 'Don’t write logs to disk',
            restart: true,
            type: 'toggle',
          },
        ],
        title: 'Logs',
      },
    ],
    icon: 'history',
    key: 'retention',
    title: 'Cleanup',
  },
  {
    description:
      'Notifications and other services. Webhooks and scripts are edited in the config file.',
    groups: [
      {
        fields: [
          {
            key: 'integrations.pushbullet.enabled',
            label: 'Send Pushbullet notifications',
            type: 'toggle',
          },
          {
            key: 'integrations.pushbullet.accessToken',
            label: 'Access token',
            secret: true,
            type: 'password',
          },
          {
            key: 'integrations.pushbullet.notifyOnPrivateMessage',
            label: 'Notify on private messages',
            type: 'toggle',
          },
          {
            key: 'integrations.pushbullet.notifyOnRoomMention',
            label: 'Notify when mentioned in a room',
            type: 'toggle',
          },
          {
            key: 'integrations.pushbullet.notificationPrefix',
            label: 'Title prefix',
            type: 'text',
          },
          {
            key: 'integrations.pushbullet.cooldownTime',
            label: 'Quiet time between notifications',
            min: 0,
            type: 'number',
            unit: 'ms',
          },
        ],
        title: 'Pushbullet',
      },
      {
        fields: [
          {
            help: 'Uploads finished downloads to an FTP server.',
            key: 'integrations.ftp.enabled',
            label: 'Upload downloads to FTP',
            type: 'toggle',
          },
          { key: 'integrations.ftp.address', label: 'Address', type: 'text' },
          {
            key: 'integrations.ftp.port',
            label: 'Port',
            max: 65_535,
            min: 1,
            type: 'number',
          },
          { key: 'integrations.ftp.username', label: 'Username', type: 'text' },
          {
            key: 'integrations.ftp.password',
            label: 'Password',
            secret: true,
            type: 'password',
          },
          {
            key: 'integrations.ftp.remotePath',
            label: 'Remote folder',
            type: 'text',
          },
          {
            key: 'integrations.ftp.encryptionMode',
            label: 'Encryption',
            options: [
              ['auto', 'Automatic'],
              ['none', 'None'],
              ['implicit', 'Implicit'],
              ['explicit', 'Explicit'],
            ],
            type: 'select',
          },
          {
            key: 'integrations.ftp.overwriteExisting',
            label: 'Overwrite existing files',
            type: 'toggle',
          },
        ],
        title: 'FTP',
      },
      {
        advanced: true,
        fields: [
          {
            key: 'integrations.vpn.enabled',
            label: 'Stay connected only while the VPN is up',
            restart: true,
            type: 'toggle',
          },
          {
            help: 'Use the port forwarded by the VPN as the listening port.',
            key: 'integrations.vpn.portForwarding',
            label: 'Use VPN port forwarding',
            type: 'toggle',
          },
          {
            key: 'integrations.vpn.gluetun.url',
            label: 'Gluetun control server URL',
            placeholder: 'http://localhost:8000',
            type: 'text',
          },
          {
            key: 'integrations.vpn.gluetun.apiKey',
            label: 'Gluetun API key',
            secret: true,
            type: 'password',
          },
        ],
        title: 'VPN (Gluetun)',
      },
    ],
    icon: 'bell',
    key: 'integrations',
    title: 'Integrations',
  },
];

export const unlimitedValue = 2_147_483_647;

export const fieldPath = (field) => field.segments ?? field.key.split('.');

export default schema;

export const userDefinedGroupsPath = ['transfers', 'groups', 'userDefined'];

export const reservedGroupNames = [
  'default',
  'leechers',
  'privileged',
  'blacklisted',
];

// user defined groups are a dictionary, so their fields are built per group
export const userGroupFields = (name) => {
  const base = [...userDefinedGroupsPath, name];

  return [
    {
      help: 'These users get this group’s upload rules instead of the regular ones.',
      key: [...base, 'members'].join('.'),
      label: 'Members',
      placeholder: 'username',
      segments: [...base, 'members'],
      type: 'list',
    },
    {
      help: 'Lower numbers are served first.',
      key: [...base, 'upload', 'priority'].join('.'),
      label: 'Priority',
      min: 1,
      segments: [...base, 'upload', 'priority'],
      type: 'number',
    },
    {
      key: [...base, 'upload', 'slots'].join('.'),
      label: 'Upload slots',
      min: 1,
      segments: [...base, 'upload', 'slots'],
      type: 'number',
      unlimited: true,
    },
    {
      key: [...base, 'upload', 'speedLimit'].join('.'),
      label: 'Speed limit',
      min: 1,
      segments: [...base, 'upload', 'speedLimit'],
      type: 'number',
      unit: kib,
      unlimited: true,
    },
    {
      key: [...base, 'upload', 'strategy'].join('.'),
      label: 'Queue order',
      options: strategyOptions,
      segments: [...base, 'upload', 'strategy'],
      type: 'select',
    },
  ];
};
