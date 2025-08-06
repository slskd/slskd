# Configuration 

The application is highly configurable while coming out of the box with sensible defaults.

The defaults that have been chosen should cover the vast majority of users. Still, deployments on low spec hardware (such as a single-board computer or a shared system) should monitor resource usage and adjust as necessary.

Credentials (username and password) for the Soulseek network are the only required configuration, but it is advised to also change the credentials for the Web UI.

# Sources

The application supports several different configuration sources to make it easy for users to deploy the application in various situations.

The configuration options used by the application derive from the values specified by each of the sources, with sources higher in the hierarchy overwriting or expanding the configuration set earlier. The hierarchy is:

```
Default Values < Environment Variables < YAML Configuration File < Command Line Arguments
```

Some options can be specified as lists. In these cases, when combining configurations from multiple sources, care should be taken. .NET framework not only appends but also overwrites values. If a list of `one;two;three` is defined in an environment variable for an option, and the YAML configuration specifies a list of `foo` and `bar`, the resulting configuration will be a list containing `foo, bar, three`.

## Default Values

Default values for each option (unless the default is `null`) are specified in `Options.cs`.  Upon startup, these values are copied into the configuration first.

## Environment Variables

Environment variables are loaded after defaults. Each environment variable name is prefixed with `SLSKD_`.

Environment variables are ideal for use cases involving Docker where configuration is expected to be changed neither often nor remotely.

Some options are backed by arrays or lists and allow multiple options to be set via a single environment variable. To achieve this, separate each list item by a semicolon `;` in the value:

```
SLSKD_SOME_OPTION=1;2;3
```

## YAML Configuration File

Configuration is loaded from the YAML file located at `<application directory>/slskd.yml` after environment variables. The location can be changed by specifying the path in the `SLSKD_CONFIG` environment variable or `--config` command-line argument.

The application watches for changes in the YAML file and will reload the configuration when they are detected. Options will be updated in real-time and transmitted to the web UI. If a server reconnect or application restart is required for changes to take effect fully, a flag will be set indicating so.

The YAML file can be read and written at run time via API calls or edited on disk.

If no such configuration file exists at startup, the example file `/config/slskd.example.yml` is copied to this location for convenience.

YAML configuration should be used in most cases.

## Command Line Arguments

Command-line arguments are loaded last, override all other configuration options, and are immutable at run time.

Command-line arguments are helpful for security options, such as remote configuration, HTTPS JWT secret, and certificate. Choosing this source for sensitive options can prevent a remote attacker from gaining full control over the application.

Some options are backed by arrays or lists and allow multiple options to be set via the command line. To achieve this, repeat the command line argument:

```
--some-option 1 --some-option 2 --some-option 3
```

# Remote Configuration

The application contains APIs for retrieving and updating the YAML configuration file. By default, this option is disabled. Applications run within untrusted networks, especially those where the application is internet-facing, should consider the risks of enabling this option.

If an attacker were to gain access to the application and retrieve the YAML file, any secrets contained within it will be exposed.

| Command-Line             | Environment Variable         | Description                                                   |
| ------------------------ | -----------------------------| ------------------------------------------------------------- |
| `--remote-configuration` | `SLSKD_REMOTE_CONFIGURATION` | Determines whether the remote configuration of options is allowed |

#### **YAML**
```yaml
remote_configuration: false
```

# Headless Mode

Users that want extra security and don't intend to use the application's web UI can disable it.  This setting is ideal in
situations where slskd will be controlled by another application via the API (like when run as a Relay agent).

| Command-Line             | Environment Variable         | Description                                                   |
| ------------------------ | -----------------------------| ------------------------------------------------------------- |
| `--headless` | `SLSKD_HEADLESS` | Determines whether the application should run in headless (no web UI) mode |

#### **YAML**
```yaml
headless: false
```

# Permissions

On [Unix-like](https://en.wikipedia.org/wiki/Unix-like) operating systems, slskd creates downloaded files with permissions dictated by the [umask](https://en.wikipedia.org/wiki/Umask) of the process, usually 022, which translates to a file mode of 644; read/write for the owner and read for the group and all others.

Users wishing to create files with different permissions can change the umask value before launching the application (e.g. `umask 000 & ./slskd`), and the file mode will be changed accordingly.

When running within a docker container the environment variable `SLSKD_UMASK` can be used to change the umask for the process within the container, and this will in turn change the resulting file mode on a mounted volume.

Users can optionally configure alternative permissions for files by setting the file permission mode option.  It's not possible to supersede the configured umask value; setting a mode of 777, for example, will have no effect with a umask of 022.

These options have no effect on Windows.

| Command-Line             | Environment Variable         | Description                                                                      |
| ------------------------ | -----------------------------| -------------------------------------------------------------------------------- |
| n/a                      | `SLSKD_UMASK`                | When running within the slskd Docker container, the umask for the process        |
| `--file-permission-mode` | `SLSKD_FILE_PERMISSION_MODE` | The permissions to apply to newly created files (chmod syntax, non-Windows only) |

#### **YAML**
```yaml
permissions:
  file:
    mode: 644 # not for Windows, chmod syntax, e.g. 644, 777. can't escalate beyond umask
```

# Application Directory Configuration

The application directory configuration option determines the location of the YAML file, the default locations of the download and incomplete directories, and the location of application working data, such as logs and SQLite databases.

Because the location of the YAML file derived from this value, it can't be specified within the YAML file and must instead be set with the `APP_DIR` environment variable or `--app-dir` command-line argument.

If no value is specified, the location defaults to either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows).

This value is set to `/app` within the official Docker image.

# Other Directory Configuration

## Incomplete and Downloads

By default, incomplete and downloaded files are saved in `APP_DIR/incomplete` and `APP_DIR/downloads` directories, respectively. The application will create these directories on startup if they don't exist.

Alternative locations can be specified for each directory. Directories must exist and be writable by the application; the application will not attempt to create them.

| Command-Line      | Environment Variable       | Description                                   |
| ----------------- | ---------------------------| --------------------------------------------- |
| `-o\|--downloads` | `SLSKD_DOWNLOADS_DIR`      | The path where downloaded files are saved     |
| `--incomplete`    | `SLSKD_INCOMPLETE_DIR`     | The path where incomplete downloads are saved |

#### **YAML**
```yaml
directories:
  incomplete: ~
  downloads: ~
```

## Remote File Management

The application offers APIs for listing and deleting files within the 'Incomplete' and 'Downloads' directories.  Listing is always allowed, while the ability
to delete is disabled by default.  Deletions can be enabled by enabling the remote file management option.

| Command-Line               | Environment Variable           | Description                                                                               |
| -------------------------- | ------------------------------ | ----------------------------------------------------------------------------------------- |
| `--remote-file-management` | `SLSKD_REMOTE_FILE_MANAGEMENT` | Determines whether the remote management of 'Incomplete' and 'Downloads' files is allowed |

#### **YAML**
```yaml
remote_file_management: false
```

# Shares

## Directories

Any number of shared directories can be configured.

Paths must be absolute, meaning they must begin with `/`, `X:\`, or `\\`, depending on the system. Relative paths, such as `~/directory` or `../directory`, are not supported. Sharing a root mount on a unix-like OS (`/`) is also not supported.

Shares can be excluded by prefixing them with `-` or `!`. This is useful in situations where sharing a subdirectory of a share isn't desired, for example, if a user wants to share their entire music library but not their personal recordings:

```yaml
shares:
  directories:
    - 'D:\Music'
    - '!D:\Music\Personal Recordings'
```

Shares can be aliased to improve privacy (for example, if a username is present in the path). A share alias can be specified by prefixing the share with the alias in square brackets, for example:

```yaml
shares:
  directories:
    - '[Music]\users\John Doe\Music'
```

If no alias is specified, the name of the shared folder is used (e.g. `D:\Music` is aliased to `Music`). To a remote user, any files within the aliased path will appear as though they are shared from a directory named as the alias.

Aliases:
* Must be unique
* Must be at least one character in length
* Must not contain path separators (`\` or `/`)

| Command-Line   | Environment Variable       | Description                       |
| -------------- | -------------------------- | --------------------------------- |
| `-s\|--shared` | `SLSKD_SHARED_DIR`         | The list of paths to shared files |

## Filters

Share filters can be used to prevent certain types of files from being shared.  This option is an array that can take any number of filters.  Filters must be a valid regular expression; a few examples are included below and in the example configuration included with the application, but the list is empty by default.

Filter expressions are case insensitive by default.

| Command Line              | Environment Variable          | Description                                                           |
| ------------------------- | ----------------------------- | --------------------------------------------------------------------- |
| `--share-filter`          | `SLSKD_SHARE_FILTER`          | A list of regular expressions used to filter files from shares        |

#### **YAML**
```yaml
shares:
  filters:
    - \.ini$
    - Thumbs.db$
    - \.DS_Store$
```

## Cache

The contents of shares are cached following a share scan, and users can choose how the cache is stored; in memory, or on disk.

Storing the cache in memory results in faster lookups and lower CPU and disk activity while the application is running, but increases the amount of memory used.  Systems that don't have a lot of memory might have an issue with large shares.

Storing the cache on disk uses quite a bit less memory and allows the application to run without issue for large shares, but lookups are slower, use more CPU, and cause more disk activity.

The cache is stored in memory by default.  If you find that the application uses too much memory or crashes with an `Out of Memory` exception, change this option to store the cache on disk.

Scanning shares is an I/O intensive operation; all of the files and directories in each share have to be listed, and each file has to be read to gather metadata like length, bitrate, and sample rate. To speed things up, multiple 'workers'
are used to allow metadata to be read from several files concurrently. The scan generally gets faster as additional workers are added, but each worker also adds additional I/O pressure and increases CPU and memory usage. At some number of workers
performance will start to get worse as more are added.  The optimal number of workers will vary from system to system, so if scan performance is important to you it will be a good idea to experiment to see what the optimal number is for your system.

The default number of workers determined by the [Environment.ProcessorCount](https://learn.microsoft.com/en-us/dotnet/api/system.environment.processorcount?view=net-6.0) property.

Shares can be configured to be automatically re-scanned at a regular interval by setting the retention limit for the cache.  This value is empty by default, and shares will not be re-scanned automatically.  The interval is set in minutes.

| Command Line                 | Environment Variable             | Description                                               |
| ---------------------------- | -------------------------------- | --------------------------------------------------------- |
| `--share-cache-storage-mode` | `SLSKD_SHARE_CACHE_STORAGE_MODE` | The type of storage to use for the cache (Memory, Disk)   |
| `--share-cache-workers`      | `SLSKD_SHARE_CACHE_WORKERS`      | The number of workers to use while scanning shares        |
| `--share-cache-retention`    | `SLSKD_SHARE_CACHE_RETENTION`    | The interval on which the cache is re-scanned, in minutes |

#### **YAML**
```yaml
shares:
  cache:
    storage_mode: memory
    workers: 4 # assuming the host has a quad core CPU
    retention: 10080 # 1 week
```

# Relay

Two (or more) instances of the application can be connected, allowing files shared by one instance to be "relayed" to another while only one of the instances is connected to the Soulseek network.  This allows users to share files from several different systems as the same Soulseek user, or to run an instance in the cloud and avoid needing to expose their home network to internet traffic.

The Relay consists of two operation modes: 'Controller', which is the copy that connects to the Soulseek network, and 'Agent', which relays files to the controller.  Only one controller can be configured, while any number of agents can be connected.

## Controller

Controllers must have at least one API key configured.  For increased security it is a good idea to use a different API key for each agent, and to specify a CIDR for each key that limits access to the known IP address range the agent will be connecting from.

The relay mode for the controller must be set to `controller`, and the relay must be enabled.

For each agent, the agent must be specified in the `agents` map, including an `instance_name` that corresponds to the top-level `instance_name` configured for the agent, and a different secret value between 16-255 characters should be specified for each agent.

It is strongly suggested that controllers be configured to force HTTPS.  This makes the traffic between the controller and agents completely private, and prevents API keys and agent secrets from being exposed.

```yaml
relay:
  enabled: true
  mode: controller
  agents:
    some_instance:
      instance_name: some_instance
      secret: <a secret value between 16 and 255 characters>
    a_different_instance:
      instance_name: different_instance
      secret: <a different secret value between 16 and 255 characters>
```

| Command-Line       | Environment Variable | Description                               |
| ------------------ | ---------------------| ------------------------------------------|
| `-r\|--relay`      | `SLSKD_RELAY`        | Enable the Relay feature                  |
| `-m\|--relay-mode` | `SLSKD_RELAY_MODE`   | The Relay mode (Controller, Agent, Debug) |

## Agents

The relay mode for agents must be set to `agent`, the relay must be enabled, and each agent must have a unique instance name that corresponds to the `instance_name` of an agent configured in the controller.

Agents need to specify the HTTP or HTTPS address of their controller, the API key for the controller, and the secret that corresponds to the value configured in the controller for the agent.

If using HTTPS; most users won't have a valid certificate (the self-signed certificates that slskd generates at startup are not 'valid' because they are self-signed), and in those cases the `ignore_certificate_errors` option should be set to `true`.

Agents can optionally receive completed downloads from the controller by enabling the `downloads` option.  With this option enabled, agents will automatically download new files from the controller and save them to the configured local downloads directory.

| Command-Line                             | Environment Variable                         | Description                                     |
| ---------------------------------------- | -------------------------------------------- | ------------------------------------------------|
| `-r\|--relay`                            | `SLSKD_RELAY`                                | Enable the Relay feature                        |
| `-m\|--relay-mode`                       | `SLSKD_RELAY_MODE`                           | The Relay mode (Controller, Agent, Debug)       |
| `--controller-address`                   | `SLSKD_CONTROLLER_ADDRESS`                   | The address of the controller                   |
| `--controller-ignore-certificate-errors` | `SLSKD_CONTROLLER_IGNORE_CERTIFICATE_ERRORS` | Ignore certificate errors                       |
| `--controller-api-key`                   | `SLSKD_CONTROLLER_API_KEY`                   | An API key for the controller                   |
| `--controller-secret`                    | `SLSKD_CONTROLLER_SECRET`                    | The shared secret for this agent                |
| `--controller-downloads`                 | `SLSKD_CONTROLLER_DOWNLOADS`                 | Receive completed downloads from the controller |

```yaml
instance_name: some_instance
relay:
  enabled: true
  mode: agent
  controller:
    address: https://my_cloud_server.example.com:5031
    ignore_certificate_errors: true
    api_key: <a valid API key for the controller instance>
    secret: <a secret value that matches the controller for this instance>
    downloads: false
```

# Limits and User Groups

## Global Slot and Speed Limits

Global slot and speed behave as a hard limit, additive across all groups. These values should be set as high as practical for the application's environment; more granular controls should be defined at the group level.

A change to slot limits requires an application restart to take effect, while speed limits can be adjusted at runtime.

| Command-Line             | Environment Variable         | Description                                      |
| ------------------------ | ---------------------------- | ------------------------------------------------ |
| `--upload-slots`         | `SLSKD_UPLOAD_SLOTS`         | The limit for the total number of upload slots   |
| `--upload-speed-limit`   | `SLSKD_UPLOAD_SPEED_LIMIT`   | The total upload speed limit, in kibibytes       |
| `--download-slots`       | `SLSKD_DOWNLOAD_SLOTS`       | The limit for the total number of download slots |
| `--download-speed-limit` | `SLSKD_DOWNLOAD_SPEED_LIMIT` | The total download speed limit, in kibibytes     |

#### **YAML**
```yaml
global:
  upload:
    slots: 20
    speed_limit: 1000 # kibibytes
  download:
    slots: 500
    speed_limit: 1000
```

## Global Upload Limits

Global upload limits define the number of queued, daily, and weekly uploads on a _per user_ basis.  These limits behave as defaults and are applied if limits for a user's group have not been set.

Limits are evaluated when a user attempts to enqueue a file.  Queued limits behave exactly as those in other clients; if a limit would be exceeded if the requested file were accepted the request is rejected with the standard messages `Too many files` or `Too many megabytes`, depending on which limit was hit.  These messages are used by other clients to determine whether to retry the transfer later.

If a daily or weekly limit would be exceeded, the request is rejected with a message like `Too many files this week` or `Too many failures today`, depending on which limit is hit.  Transfers rejected with one of these messages should not be retried by other clients (at least, that's the goal!).

The transfers database for the application is used to calculate the statistics used to make limit decisions.  If this database is deleted, moved, cleared, or if the 'volatile' option is set (preventing the database from being persisted to disk), these limits won't work reliably.  Because of the asynchronous nature of the application it's possible for limits to be exceeded if the requests are 'in flight' while data is being persisted, but the number of excess transfers should never be significant.

Note that while it's possible to set the `failures` option for `queued`, it has no effect.

#### **YAML**
```yaml
global:
  limits:
    queued:
      files: 500
      megabytes: 5000
    daily:
      files: 1000
      megabytes: 10000
      failures: 200
    weekly:
      files: 5000
      megabytes: 50000
      failures: 1000
```

## Groups

User groups are used to control upload slots, speed limits, and queue behavior on a per-user basis.

Each group has a priority, starting from 1, determining the order in which groups are prioritized in the upload queue. A lower number translates to higher priority, and Upload slots are granted to higher priority groups before lower priority groups.

Groups have a queue strategy, either `FirstInFirstOut` or `RoundRobin`. This setting determines how uploads from multiple users in the same group are processed; `FirstInFirstOut` processes uploads in the order in which they were enqueued, while `RoundRobin` processes uploads in the order in which the user was ready to receive the upload.

[Upload slots and speed limits](#global-slot-and-speed-limits) configured at the group level can be used to create constraints in addition to global settings. If group-level limits exceed global settings, global settings become the constraint. Slots and speed limit settings default to `int.MaxValue`, effectively deferring to the global limits.

[Upload limits](#global-upload-limits) can be configured at the group level to override the global defaults.  If neither global defaults nor group limits are set, uploads are unlimited.  If global defaults are set and unlimited uploads for a group are desired, set values to `int.MaxValue` or `2147483647`.  While not technically unlimited, this number translates to over 2 petabytes, which would be an impressive amount of data to move peer to peer in a week.

The general configuration for a group is as follows:

#### **YAML**
```yaml
upload:
  priority: 500 # 1 to 2147483647
  strategy: roundrobin # roundrobin or firstinfirstout
  slots: 10 # 1 to 2147483647
  speed_limit: 100 # in KiB/s, 1 to 2147483647
limits:
  queued:
    files: 500 # 1 to 2147483647
    megabytes: 5000 # 1 to 2147483647
  daily:
    files: 1000
    megabytes: 10000
    failures: 200 # 1 to 2147483647
  weekly:
    files: 5000
    megabytes: 50000
    failures: 1000
```

## Built-In Groups

The `default` built-in group contains all users who have not been explicitly added to a user-defined group, are not privileged, and haven't been identified as leechers.

The `leechers` built-in group contains users that have not been explicitly added to a user-defined group, are not privileged, and have shared file and/or directory counts less than the configured `thresholds` for the group. By default, users must share at least one directory with one file to avoid being identified as leechers.

The `privileged` built-in is used to prioritize users who have purchased privileges on the Soulseek network. This groups is not configurable, has a priority of 0 (the highest), a strategy of `FirstInFirstOut`, and can use any number of slots up to the global limit.  Users in this group are also not subject to any limits; they can enqueue any number of files they wish.

It is impossible to explicitly assign users to these built-in groups, but the priority, number of slots, speed, and queue strategy can be adjusted (excluding `privileged`).

#### **YAML**
```yaml
groups:
  default:
    upload:
      priority: 1
      strategy: roundrobin
      slots: 10
      speed_limit: 50000 # kibibytes
    limits:
      queued:
        files: 150
        megabytes: 1500
      daily:
        files: 2147483647 # effectively unlimited, weekly still applies
        megabytes: 2147483647
      weekly:
        files: 1500
        megabytes: 15000
        failures: 150
  leechers:
    thresholds:
      files: 1
      directories: 1
    upload:
      priority: 99
      strategy: roundrobin
      slots: 1
      speed_limit: 100
    limits:
      queued:
        files: 15
        megabytes: 150
      daily:
        files: 30
        megabytes: 300
        failures: 10
      weekly:
        files: 150
        megabytes: 1500
        failures: 30
```

## User Blacklist

A fourth built-in group, `blacklisted`, is used to disallow other users from various activities.

Blacklisted users are prevented from:
- Receiving search results
- Browsing files
- Retrieving directory contents
- Enqueueing downloads

Private and chat room messages from blacklisted users are also ignored.

Users can be blacklisted by adding their username to the `members` list. Additionally, users can be blacklisted by IP address, or range of addresses by adding a CIDR entry to the `cidrs` list.

Users added to the blacklist will be blocked from enqueueing any new files.  Any existing active or queued transfers will need to be cancelled manually.

A managed blacklist file may also be used to achieve the same effects.  Read more about this in the [Managed Blacklist](#managed-blacklist) section below.

**YAML**
```yaml
groups:
  blacklisted:
    members:
      - <username to blacklist>
    cidrs:
      - <CIDR to blacklist, e.g. 255.255.255.255/32>
```

## User Defined Groups

In the group configuration, any number of user-defined groups can be added under the `user_defined` key.

User-defined groups use the same configuration as built-in groups and allow a list of `members` containing the usernames of users assigned to the group.

Users can be assigned to multiple groups, with their effective group being the highest priority (lowest-numbered) group. If a user has privileges on the network, any explicit group membership is superseded, and their effective group is the built-in `privileged` group. If users are explicitly assigned to a group, they will not be identified as leechers.

#### **YAML**
```yaml
groups:
  user_defined:
    my_custom_group_name:
      upload:
        priority: 500
        strategy: roundrobin
        slots: 10
        speed_limit: 100
      limits:
        queued:
          files: 1000 # override global default
      members:
        - bob
        - alice
```

## Example

In the following example:

* All leechers share one slot and can download at a maximum speed of 100 KiB/s.  Leechers can only download if fewer than 20 other uploads are in progress to other users.
* Users that aren't leechers and that aren't in the `my_buddies` group (`default` users) share 10 slots among them and can download at the global maximum speed of 1000 KiB/s, but only if fewer than 20 upload slots are being used by users in the `my_buddies` group.
* Users in the `my_buddies` group share 20 slots among them and can download at the global maximum speed. Ten upload slots are reserved for users in this group, and users `alice` and `bob` are members.

```yaml
global:
  upload:
    slots: 20
    speed_limit: 1000
  download:
    slots: 500
    speed_limit: 1000
groups:
  default:
    upload:
      priority: 500
      strategy: roundrobin
      slots: 10
  leechers:
    upload:
      priority: 999
      strategy: roundrobin
      slots: 1
      speed_limit: 100
  user_defined:
    my_buddies:
      upload:
        priority: 250
        queue_strategy: firstinfirstout
        slots: 20
      members:
        - alice
        - bob
```

## Managed Blacklist

A managed blacklist (also called a blocklist) can be specified to disallow interaction with known undesirable actors.  Users whose IP address matches an entry on this list are functionally the same as users added to the [User Blacklist](#user-blacklist) described above and are disallowed any interactions with shares or files.

Blacklists/blocklists designed for use with other P2P applications (such as BitTorrent) are supported; specifically the P2P, DAT, and CIDR formats.

When enabled, the specified file is validated by reading the first few lines of the file to automatically detect the format.  If the file doesn't exist, can't be read, the format can't be detected, or if it contains any lines that don't match the format, the application will exit.  Similarly, if the file changes while the application is running and there's an issue, the application will exit.

Note that the contents of the blacklist are stored in memory to ensure performance, and this will increase the amount of memory used, potentially significantly if the blacklist is large.

| Command-Line         | Environment Variable | Description                    |
| -------------------- | -------------------- | ------------------------------ |
| `--enable-blacklist` | `BLACKLIST`          | Enable the managed blacklist   |
| `--blacklist-file`   | `BLACKLIST_FILE`     | The path to the blacklist file |

```yaml
blacklist:
  enabled: true
  file: <path to file containing CIDRs to blacklist>
```

# Chat Rooms and Private Messaging

## Auto-Joining Rooms

A configured list of chat rooms will now be automatically joined on connect.

The list can be specified via command line (e.g. --rooms <room1> --rooms <room2>), with the SLSKD_ROOMS environment variable (rooms separated by a semicolon).

Additionally, when the client is disconnected, any rooms that were joined at the time of the disconnect are rejoined upon reconnect, as long as the app hasn't been restarted in between.

| Command-Line  | Environment Variable | Description                             |
| ------------- | ---------------------| ----------------------------------------|
| `--rooms`     | `ROOMS`              | A list of chat rooms to join on startup |

#### **YAML**
```yaml
rooms:
  - <room 1>
  - <room 2>
```


# Soulseek Configuration

The Soulseek configuration determines how slskd interacts with the Soulseek network and underlying [Soulseek.NET](https://github.com/jpdillingham/Soulseek.NET) library.

## Server Connection

By default the application connects to `vps.slsknet.org` on port `2271`.  This should work for nearly everyone, but it is possible to override both defaults if necessary.

| Command-Line     | Environment Variable | Description                        |
| ---------------- | ---------------------| -----------------------------------|
| `--slsk-address` | `SLSKD_SLSK_ADDRESS` | The address of the Soulseek server |
| `--slsk-port`    | `SLSKD_SLSK_PORT`    | The port of the Soulseek server    |

#### **YAML**
```yaml
soulseek:
  address: vps.slsknet.org
  port: 2271
```

## Username and Password

Credentials to log in to the Soulseek network.

Changing either of these values requires the server connection to be reset.

The password field is masked when serializing options to JSON or YAML to avoid inadvertently exposing the value.

| Command-Line      | Environment Variable       | Description                           |
| ----------------- | -------------------------- | ------------------------------------- |
| `--slsk-username` | `SLSKD_SLSK_USERNAME`      | The username for the Soulseek network |
| `--slsk-password` | `SLSKD_SLSK_PASSWORD`      | The password for the Soulseek network |

#### **YAML**
```yaml
soulseek:
  username: <username>
  password: <password>
```

## Distributed Network

Options for the Soulseek distributed network, which is how search requests are delivered.

The distributed network should only be disabled if no files are being shared.  

Child connections should generally only be disabled on low spec systems or situations where network bandwidth is scarce. Received search requests are re-broadcast to each child connection, and incoming requests are numerous. Consider increasing the child limit from the default of 25 on systems with CPU and memory headroom.

Depending on the current state, changing these values may require the server connection to be reset.

| Command-Line              | Environment Variable    | Description                                                                                                                                                                           |
| ------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--slsk-no-dnet`          | `SLSKD_SLSK_NO_DNET`          | Determines whether the distributed network is disabled. If disabled, the client will not obtain a parent or any child connections, and will not receive distributed search requests. |
| `--slsk-dnet-no-children` | `SLSKD_SLSK_DNET_NO_CHILDREN` | Determines whether to disallow distributed children                                                                                                                                   |
| `--slsk-dnet-children`    | `SLSKD_SLSK_DNET_CHILDREN`    | The maximum number of distributed children to accept                                                                                                                                  |
| `--slsk-dnet-logging`     | `SLSKD_SLSK_DNET_LOGGING`     | Determines whether to enable distributed network logging

#### **YAML**
```yaml
soulseek:
  distributed_network:
    disabled: false
    disable_children: false
    child_limit: 25
```

## Listen IP Address and Port

The local IP address and port on which the application listens for incoming connections.

As with any other Soulseek client, configuring the listen port and port forwarding ensures full connectivity with other clients, including those without a correctly configured a listening port.  

Symptoms of a misconfigured listen port include poor search results and the inability to browse or retrieve user information for some users.

The local IP address defaults to 0.0.0.0, and this should be correct for most people.  If you have multiple network adapters or are using a VPN you may want to set this value to ensure
incoming traffic is properly routed.

| Command-Line                | Environment Variable           | Description                                                      |
| --------------------------- | ------------------------------ | ---------------------------------------------------------------- |
| `--slsk-listen-ip-address`  | `SLSKD_SLSK_LISTEN_IP_ADDRESS` | The local IP address on which to listen for incoming connections |
| `--slsk-listen-port`        | `SLSKD_SLSK_LISTEN_PORT`       | The port on which to listen for incoming connections             |

#### **YAML**
```yaml
soulseek:
  listen_ip_address: 0.0.0.0
  listen_port: 50300
```

## Other

Users can configure "profile" information for other users on the network to view, including a description and a photo.  Note that Soulseek NS doesn't support .PNG,
so formats .JPG/.JPEG, .GIF, and .BMP are advised.

| Command-Line         | Environment Variable       | Description                                   |
| -------------------- | -------------------------- | --------------------------------------------- |
| `--slsk-description` | `SLSKD_SLSK_DESCRIPTION`   | The user description for the Soulseek network |
| `--slsk-picture`     | `SLSKD_SLSK_PICTURE`       | The user picture for the Soulseek network     |

#### **YAML**
```yaml
soulseek:
  description: A slskd user. https://github.com/sredevopsorg/slskd
  picture: path/to/slsk-profile-picture.jpg
```

## Connection Options

### Timeouts

Timeout options control how long the application waits for connections to connect, and how long connections can be inactive before they are disconnected.

Higher connect timeout values will help ensure that operations (browse, download requests, etc.) are successful the first time but decrease the responsiveness of commands that will ultimately fail.

Inactivity timeouts help the application determine when a distributed parent connection has stopped sending data and when connections that have delivered search results (and are unlikely to be used further) from remaining open longer than needed. Reducing this timeout can help low spec systems if port exhaustion is a concern but may result in the application "hunting" for a distributed parent connection needlessly.

| Command-Line                | Environment Variable            | Description                                      |
| --------------------------- | ------------------------------- | ------------------------------------------------ |
| `--slsk-connection-timeout` | `SLSKD_SLSK_CONNECTION_TIMEOUT` | The connection timeout value, in milliseconds    |
| `--slsk-inactivity-timeout` | `SLSKD_SLSK_INACTIVITY_TIMEOUT` | The connection inactivity value, in milliseconds |

#### **YAML**
```yaml
soulseek:
  connection:
    timeout:
      connect: 10000
      inactivity: 15000  
```

### Buffers

Buffer options control the application's internal buffer to batch socket reads and writes. The host Operating System controls actual socket buffer sizes (.NET is not great in this department currently, and the behavior is not consistent cross-platform).

Larger buffer sizes can improve performance, especially for file transfers, resulting in increased memory usage.

The transfer buffer size is used for file transfers (both read and write), and the read and write buffer sizes are used for all other connection types. The transfer buffer size directly correlates with transfer speed; setting this value much lower than the default will result in slow uploads (which may be good for low-spec hardware).

The write queue option is the hard limit for the number of concurrent writes for a connection. It generally only applies to distributed child connections and prevents a memory leak if the application continues to try and send data after a connection has gone "bad". This value can be set as low as five if memory is a constraint, though the default has been tested extensively and should be suitable for most scenarios, including low spec.

| Command-Line             | Environment Variable         | Description                                        |
| ------------------------ | ---------------------------- | -------------------------------------------------- |
| `--slsk-read-buffer`     | `SLSKD_SLSK_READ_BUFFER`     | The connection read buffer size, in bytes          |
| `--slsk-write-buffer`    | `SLSKD_SLSK_WRITE_BUFFER`    | The connection write buffer size, in bytes         |
| `--slsk-transfer-buffer` | `SLSKD_SLSK_TRANSFER_BUFFER` | The read/write buffer size for transfers, in bytes |
| `--slsk-write-queue`     | `SLSKD_SLSK_WRITE_QUEUE`     | The connection write buffer size, in bytes         |

#### **YAML**
```yaml
soulseek:
  connection:
    buffer:
      read: 16384
      write: 16384
      transfer: 262144
      write_queue: 250    
```

### Proxy

Connections can optionally use a SOCKS5 proxy, with or without username and password authentication.

An address and port must also be specified if the proxy is enabled.

| Command-Line            | Environment Variable        | Description                              |
| ----------------------- | --------------------------- | ---------------------------------------- |
| `--slsk-proxy`          | `SLSKD_SLSK_PROXY`          | Determines whether a proxy is to be used |
| `--slsk-proxy-address`  | `SLSKD_SLSK_PROXY_ADDRESS`  | The proxy address                        |
| `--slsk-proxy-port`     | `SLSKD_SLSK_PROXY_PORT`     | The proxy port                           |
| `--slsk-proxy-username` | `SLSKD_SLSK_PROXY_USERNAME` | The proxy username, if applicable        |
| `--slsk-proxy-password` | `SLSKD_SLSK_PROXY_PASSWORD` | The proxy password, if applicable        |

#### **YAML**
```yaml
soulseek:
  connection:
    proxy:
      enabled: false
      address: ~
      port: ~
      username: ~
      password: ~   
```

## Diagnostic Level

The diagnostic level option is passed to the Soulseek.NET configuration and determines the level of detail the library produces diagnostic messages. This option should generally be left to `Info` or `Warning` but can be set to `Debug` if more verbose logging is desired.

| Command-Line        | Environment Variable       | Description                                               |
| ------------------- | -------------------------- | --------------------------------------------------------- |
| `--slsk-diag-level` | `SLSKD_SLSK_DIAG_LEVEL`    | The minimum diagnostic level (None, Warning, Info, Debug) |

#### **YAML**
```yaml
soulseek:
  diagnostic_level: Info
```

# Web Configuration

## Basic

The default HTTP listen port is 5030, but can be anything between 1 and 65535.

The URL base option allows the application to operate behind a reverse proxy. Setting a base of "slskd" would make the web UI accessible at `http://<host>:<port>/slskd`.

The content path can be used to force the application to serve static web content from a location other than the default (`wwwroot`). The application is designed to decouple the Web UI from the rest of the application to be replaceable.

Logging of HTTP requests is disabled by default.

| Command-Line      | Environment Variable       | Description                                       |
| ----------------- | -------------------------- | ------------------------------------------------- |
| `-l\|--http-port` | `SLSKD_HTTP_PORT`          | The HTTP listen port                              |
| `--url-base`      | `SLSKD_URL_BASE`           | The base url for web requests                     |
| `--content-path`  | `SLSKD_CONTENT_PATH`       | The path to static web content                    |
| `--http-logging`  | `SLSKD_HTTP_LOGGING`       | Determines whether HTTP requests are to be logged |

#### **YAML**
```yaml
web:
  port: 5030
  url_base: /
  content_path: wwwroot
  logging: false
```

## HTTPS

The default HTTPS port is 5031, but can be anything between 1 and 65535.

By default, the application generates a new, self-signed X509 certificate at each startup. If, for whatever reason, a self-signed certificate isn't sufficient, or if the certificate needs to be shared among systems or applications, a certificate `.pfx` and password can be defined.

The application can produce a self-signed `.pfx` file and random password using the `--generate-cert` command.

| Command-Line            | Environment Variable        | Description                                                    |
| ----------------------- | --------------------------- | -------------------------------------------------------------- |
| `-L\|--https-port`      | `SLSKD_HTTPS_PORT`          | The HTTPS listen port                                          |
| `-f\|--force-https`     | `SLSKD_HTTPS_FORCE`         | Determines whether HTTP requests are to be redirected to HTTPS |
| `--https-cert-pfx`      | `SLSKD_HTTPS_CERT_PFX`      | The path to the X509 certificate .pfx file                     |
| `--https-cert-password` | `SLSKD_HTTPS_CERT_PASSWORD` | The password for the X509 certificate                          |
| `--no-https`            | `SLSKD_NO_HTTPS`            | Determines whether HTTPS is to be disabled                     |

#### **YAML**
```yaml
web:
  https:
    disabled: false
    port: 5031
    force: false
    certificate:
      pfx: ~
      password: ~
```

## Authentication

Authentication for the web UI (and underlying API) is enabled by default, and the default username and password are both `slskd`. Changing both the username and password during the initial configuration is highly recommended.

By default, a random JWT secret key is generated at each start. It is convenient and secure, but restarting the application will invalidate any issued JWTs, causing users to sign in again. To avoid this, supply a custom secret at least 16 characters in length. Note that the secret can be used to generate valid JWTs for the application, so keep this value secret.

The JWT TTL option determines how long issued JWTs are valid, defaulting to 7 days.

API keys can be configured to allow for secure communication without requiring the caller to obtain a JWT by signing in with a username and password. Each key must be given a name and a key with a length between 16 and 255 characters (inclusive). Callers may then supply one of the configured keys in the `X-API-Key` header when making web requests. Remember that API keys are secrets, so keep them safe.

You can generate a random, 32 bit API key by starting the application with `-k` or `--generate-secret` at the command line.

An optional comma separated list of [CIDRs](https://en.wikipedia.org/wiki/Classless_Inter-Domain_Routing) can be defined for each key, which will restrict usage of the key to callers with a remote IP address that falls within one of the defined CIDRs.  The default CIDR list for each key is `0.0.0.0/0,::0`, which applies to any IP address (IPv4 or IPv6).

A common use case for CIDR filtering might be to restrict API access to clients within your home network. Assuming your network uses the common `192.168.1.x` addressing, you could specify a `cidr` of `192.168.1.0/24`, which would apply to any IP between `192.168.1.1` and `192.168.1.254`, inclusive.

Note that CIDR filtering may not work as expected behind a reverse proxy, ingress controller, or load balancer, because the remote IP address will be that of the device that's handling ingress.  This application doesn't support the `X-Forwarded-For` header (or anything like it) because a bad actor can easily fake it.  If you wish to use CIDR filtering in this scenario, you'll need to do it at the point of ingress to your network.

Please also note that using API key authentication without HTTPS is **NOT RECOMMENDED**.  API keys are sent in HTTP headers (and in the case of SignalR, in query parameters) and will be easily accessible to anyone eavesdropping on the network.  This is a risk with JWTs as well, but JWTs expire and API keys don't.  If you choose to use API keys over plain HTTP, seriously consider using CIDR filtering.

| Command-Line     | Environment Variable       | Description                                         |
| ---------------- | -------------------------- | --------------------------------------------------- |
| `-X\|--no-auth`  | `SLSKD_NO_AUTH`            | Determines whether authentication is to be disabled |
| `-u\|--username` | `SLSKD_USERNAME`           | The username for the web UI                         |
| `-p\|--password` | `SLSKD_PASSWORD`           | The password for the web UI                         |
| `--jwt-key`      | `SLSKD_JWT_KEY`            | The secret key used to sign JWTs                    |
| `--jwt-ttl`      | `SLSKD_JWT_TTL`            | The TTL (duration) of JWTs, in milliseconds         |

#### **YAML**
```yaml
web:
  authentication:
    disabled: false
    username: slskd
    password: slskd
    jwt:
      key: ~
      ttl: 604800000
    api_keys:
      my_api_key:
        key: <some example string between 16 and 255 characters>
        cidr: 0.0.0.0/0,::/0
```

# Filters

A number of filters can be configured to control various aspects of how the application interacts with the Soulseek network.

## Search Filters

Search filters can be used to prevent certain types of search requests from being performed against your shares.  This option is an array that can take any number of filters.  Filters must be a valid regular expression; a few examples are included below and in the example configuration included with the application, but the list is empty by default.

Filter expressions are case insensitive by default.

| Command Line              | Environment Variable    | Description                                                           |
| ------------------------- | ----------------------- | --------------------------------------------------------------------- |
| `--search-request-filter` | `SEARCH_REQUEST_FILTER` | A list of regular expressions used to filter incoming search requests |

#### **YAML**
```yaml
filters:
  search:
    request:
      - ^.{1,2}$ # discard any requests shorter than 3 characters
      - ^(\.?pdf|\.?docx|\.?xlsx)$ # discard any requests that might be looking for sensitive documents
```

# Data Retention

By default, most things created by the application are retained indefinitely; they have to be removed manually by the user.  Users can optionally configure certain things to be removed or deleted automatically after a period of time.

Completed searches can be configured to be removed from the UI and 'hard' deleted from the database.  It's a good idea to set this to something relatively short, as old searches have diminishing value as they age and the records contain a lot of data.

Transfers can be configured to be removed from the UI after they are complete by specifying retention periods for uploads and downloads separately, and by transfer state; succeeded, errored, and cancelled.

Files (on disk) can be configured to be deleted after the age of their last access time exceeds the configured time.  Completed and incomplete files can be configured separately.

Application logs are removed after 180 days by default, but this can be configured as well.

All retention periods are specified in minutes, with the exception of `logs`, which is in days.

#### **YAML**
```yaml
retention:
  search: 1440 # 1 day
  transfers:
    upload:
      succeeded: 1440 # 1 day
      errored: 30
      cancelled: 5
    download:
      succeeded: 1440 # 1 day
      errored: 20160 # 2 weeks 
      cancelled: 5
  files:
    complete: 20160 # 2 weeks
    incomplete: 43200 # 30 days
  logs: 180 # days
```

# Integrations

## User-Defined

User-defined integrations allow users to configure external applications to receive data from slskd as things happen internally.  slskd uses an internal [event bus](https://www.akamai.com/glossary/what-is-an-event-bus) over which event data is sent from application logic to whatever destination(s) users choose.

The number and type of events will change as the application matures, but each event should have some minimal properties:

|Name|Description|
|-|-|
|Id|The unique ID for the event.  Events may be delivered to external consumers more than once; use this property to de-duplicate them.|
|Timestamp|The time (UTC) at which the event was raised.  Events can arrive out of chronological order; use this property to determine if that has taken place.|
|Version|The event version, used to disambiguate event shapes if a breaking change is made to the data in the future.|
|Type|The event type.  Use this to determine what other data will be included.  See: [Events.cs](https://github.com/sredevopsorg/slskd/blob/master/src/slskd/Events/Types/Events.cs).|

Here's an example of what a `DownloadFileComplete` event (at the time of this writing; review Events.cs!) looks like:

```json
{
    "type": "DownloadFileComplete",
    "version": 0,
    "localFilename": "asdfasfdasfdlocal.file",
    "remoteFilename": "asdfasfdasfdremote.file",
    "transfer": {
        "id": "00000000-0000-0000-0000-000000000000",
        "direction": "Download",
        "size": 0,
        "startOffset": 0,
        "state": "None",
        "requestedAt": "0001-01-01T00:00:00",
        "bytesTransferred": 0,
        "averageSpeed": 0,
        "bytesRemaining": 0,
        "percentComplete": 0
    },
    "id": "973bed18-8516-4b7d-ab24-e13888385237",
    "timestamp": "2024-11-28T00:54:13.8939864Z"
}
```

Users developing integrations can use the internal `RaiseEvent` HTTP endpoint to create sample events and put them on the bus.  This will help test integrations end to end without having to take action within the application, but be aware; using this endpoint after testing is complete is not a good idea!

Here's an example cURL command for the endpoint:

```bash
curl --location 'localhost:5030/api/v0/events/downloadfilecomplete' \
  --header 'Content-Type: application/json' \
  --header 'X-API-Key: <an API key from your config>' \
  --data '"asdfasfdasfd"'
```

The data passed is a 'disambiguator' that's prepended to sample data.  This helps differentiate between different event instances.

### Webhooks

Webhooks (outbound HTTP requests) can be configured to be called when application events are raised.  Webhooks are given a name (useful for troubleshooting!), a list of triggering events, a `call` configuration that defines the HTTP request to be made, and timeout and retry configuration.

Webhook HTTP requests are always a `POST`, and will always include the event data in the body, serialized as JSON.  Users define a fully-qualified endpoint address, an optional collection of headers, and an optional flag to ignore HTTPS errors caused by invalid (e.g. self signed) certificates.

If unconfigured, the default timeout for requests is 5 seconds, and each request will be attempted only once per event.

#### **YAML**
```yaml
integration:
  webhooks:
    my_basic_webhook_using_defaults:
      on:
        - All
      call:
        url: http://localhost:4224
    my_webhook_using_detailed_config:
      on:
        - DownloadFileComplete
      call:
        url: https://192.168.1.42:8080/slskd_webhook
        headers:
          - name: X-API-Key # if using API key style authentication
            value: foobar1234
          - name: Authorization # if using JWT authentication
            value: Bearer eyJ...ssw5c
          - name: User-Agent # can be useful!
            value: slskd/0.0
        ignore_certificate_errors: true # defaults to false
      timeout: 5000 # in milliseconds
      retry:
        attempts: 3
```

### Scripts

User-defined scripts can be configured to run when application events are raised.  Scripts are given a name (useful for troubleshooting!), a list of triggering events, and a `run` configuration that's used to execute the script.

The `run` configuration can take a few different forms (see examples below), but in all cases a new process is started using either the `executable` you specify or, if you don't specify one, the executable set in your system's `$SHELL` environment variable, or `cmd.exe` on Windows.

Processes are started in the `/scripts` directory (meaning the working directory), and stdout and stderr are redirected to slskd for logging purposes.  Depending on the `run` configuration your defined command will be passed as either a single argument or list of arguments, with the latter being easier to properly quote and escape.

The data associated with the event that's invoking your script is stringified to JSON and set as the value of the `SLSKD_SCRIPT_DATA` environment variable.  Whatever command or script you specify will need to read this value and handle the data.

> [!CAUTION]
> **Remote Code Execution Risk**: The event data in `$SLSKD_SCRIPT_DATA` originates from the Soulseek network and may contain malicious content. Passing this data as command-line arguments could allow attackers to inject shell commands through specially crafted filenames, usernames, or other fields. For example, a filename like `song"; rm -rf /; echo "` could delete your entire filesystem if passed unsafely to a shell command.
> 
> **Best practices:**
> - ✅ Read `$SLSKD_SCRIPT_DATA` as an environment variable
> - ✅ Parse the JSON and validate/sanitize individual fields before use
> - ✅ Use programming languages with proper JSON parsing (Python, Node.js, etc.)
> - ❌ Avoid using `$SLSKD_SCRIPT_DATA` in shell command substitution like `$(echo $SLSKD_SCRIPT_DATA)`
> - ❌ Avoid passing the raw data as arguments: `my_script.sh "$SLSKD_SCRIPT_DATA"`

#### **YAML**
```yaml
integration:
  scripts:
    my_post_download_script:
      on:
        - DownloadFileComplete
        - DownloadDirectoryComplete
      run:
        command: "echo "new event!" >> event_log.txt # command is run with system $SHELL/cmd.exe on Windows
    my_logging_script:
      on:
        - All
      run:
        executable: /bin/sh # explicitly setting executable; if omitted, $SHELL/cmd.exe is used
        args: '-c "echo "new event!" >> event_log.txt"'
    another_script:
      on:
        - All
      run:
        executable: /bin/sh # as above, can omit to use $SHELL/cmd.exe
        arglist: # easier to properly quote
          - /c
          - 'echo "new event!" >> event_log.txt'
```

## System-Defined

System-defined integrations are part of the application logic.  Users can configure settings, but don't have any control over behavior.

### FTP

Files can be uploaded to a remote FTP server upon completion. Files are uploaded to the server and remote path specified using the directory and filename with which they were downloaded; the FTP will match the layout of the local disk.

Uploads are attempted up to the maximum configured retry count and then discarded.

| Command-Line                      | Environment Variable                  | Description                                              |
| --------------------------------- | ------------------------------------- | -------------------------------------------------------- |
| `--ftp`                           | `SLSKD_FTP`                           | Determines whether FTP integration is enabled            |
| `--ftp-address`                   | `SLSKD_FTP_ADDRESS`                   | The FTP address                                          |
| `--ftp-port`                      | `SLSKD_FTP_PORT`                      | The FTP port                                             |
| `--ftp-username`                  | `SLSKD_FTP_USERNAME`                  | The FTP username                                         |
| `--ftp-password`                  | `SLSKD_FTP_PASSWORD`                  | The FTP password                                         |
| `--ftp-remote-path`               | `SLSKD_FTP_REMOTE_PATH`               | The remote path for uploads                              |
| `--ftp-encryption-mode`           | `SLSKD_FTP_ENCRYPTION_MODE`           | The FTP encryption mode (none, implicit, explicit, auto) |
| `--ftp-ignore-certificate-errors` | `SLSKD_FTP_IGNORE_CERTIFICATE_ERRORS` | Determines whether to ignore FTP certificate errors      |
| `--ftp-overwrite-existing`        | `SLSKD_FTP_OVERWRITE_EXISTING`        | Determines whether existing files should be overwritten  |
| `--ftp-connection-timeout`        | `SLSKD_FTP_CONNECTION_TIMEOUT`        | The connection timeout value, in milliseconds            |
| `--ftp-retry-attempts`            | `SLSKD_FTP_RETRY_ATTEMPTS`            | The number of times failing uploads will be retried      |

#### **YAML**
```yaml
integration:
  ftp:
    enabled: false
    address: ~
    port: ~
    username: ~
    password: ~
    remote_path: /
    encryption_mode: auto
    ignore_certificate_errors: false
    overwrite_existing: true
    connection_timeout: 5000
    retry_attempts: 3
```

### Pushbullet

Pushbullet notifications can be sent when a private message is sent, or the current user's username is mentioned in a chat room. Notifications are prefixed with a user-definable string to differentiate these notifications from others.  

A Pushbullet account must be created, and users must create an API key within the Pushbullet application and configure it through options. Complete documentation for the Pushbullet API, including the latest instructions for obtaining an API key or "Access Token" can be found [here](https://docs.pushbullet.com/).

The Pushbullet integration is one-way, meaning the application cannot know whether a user is active or receiving notifications. To prevent an inappropriate number of notifications from being sent, for example, if a user is carrying on an active conversation, a "cooldown" option is provided to ensure that notifications are sent only after the cooldown has expired. By default, this is every 15 minutes.

Notification API calls are made up to the maximum configured retry count and then discarded.

| Command-Line                          | Environment Variable                         | Description                                                                                        |
| ------------------------------------- | -------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `--pushbullet`                        | `SLSKD_PUSHBULLET`                           | Determines whether Pushbullet integration is enabled                                               |
| `--pushbullet-token`                  | `SLSKD_PUSHBULLET_TOKEN`                     | The Pushbullet API access token                                                                    |
| `--pushbullet-prefix`                 | `SLSKD_PUSHBULLET_PREFIX`                    | The prefix for notification titles                                                                 |
| `--pushbullet-notify-on-pm`           | `SLSKD_PUSHBULLET_NOTIFY_ON_PRIVATE_MESSAGE` | Determines whether to send a notification when a private message is received                       |
| `--pushbullet-notify-on-room-mention` | `SLSKD_PUSHBULLET_NOTIFY_ON_ROOM_MENTION`    | Determines whether to send a notification when the current user's name is mentioned in a chat room |
| `--pushbullet-retry-attempts`         | `SLSKD_PUSHBULLET_RETRY_ATTEMPTS`            | The number of times failing API calls will be retried                                              |
| `--pushbullet-cooldown`               | `SLSKD_PUSHBULLET_COOLDOWN_TIME`             | The cooldown time for notifications, in milliseconds                                               |

#### **YAML**
```yaml
integration:
  pushbullet:
    enabled: false
    access_token: ~
    notification_prefix: "From slskd:"
    notify_on_private_message: true
    notify_on_room_mention: true
    retry_attempts: 3
    cooldown_time: 900000
```

# Other Configuration

## Instance Name

The instance name uniquely identifies the running instance of the application. It is primarily helpful for structured logging in cases where multiple instances are logging to the same remote source.

| Command-Line          | Environment Variable       | Description                              |
| --------------------- | -------------------------- | ---------------------------------------- |
| `-i\|--instance-name` | `SLSKD_INSTANCE_NAME`      | The unique name for the running instance |

#### **YAML**
```yaml
instance_name: default
```

## Logging Options and Configurable Loggers

By default, the application logs to console with colors enabled.  Logging to disk is optional, and if enabled, logs will be written to `/logs` in the application directory.

Console colors can be disabled via typical application configuration described below, or by setting the environment variable `NO_COLOR` in accordance with https://no-color.org/.

| Command Line       | Environment Variable           | Description                        |
| ------------------ | ------------------------------ | ---------------------------------- |
| `--disk-logger`    | `SLSKD_DISK_LOGGER`            | Enable logging to disk             |
| `--no-color`       | `SLSKD_NO_COLOR` or `NO_COLOR` | Disable console log colors         |

Logs can optionally be forwarded to external services, and the targets can be expanded to any service supported by a [Serilog Sink](https://github.com/serilog/serilog/wiki/Provided-Sinks). Support for targets is added on an as-needed basis and within reason.

The current list of available targets is:

| Command Line       | Environment Variable       | Description                        |
| ------------------ | -------------------------- | ---------------------------------- |
| `--loki`           | `SLSKD_LOKI`               | The URL to a Grafana Loki instance |

#### **YAML**
```yaml
logger:
  loki: ~
  disk: false
  no_color: false
```

## Metrics

The application captures metrics internally and can optionally expose these metrics to be consumed by an instance of Prometheus.  This is a good option for those wanting to tune performance characteristics of the application.

Metrics are disabled by default, and enabling them will make them available at `/metrics`.  Authentication is enabled by default, and the credentials are the same defaults as the web UI (`slskd`:`slskd`).

If the application will be exposed to the internet, it's a good idea to leave this disabled or to set credentials other than the defaults.  Elements of the system configuration, like operating system, architecture, and drive configuration are included and can this can make it easier for an attacker to exploit a vulnerability in your system.


| Command Line         | Environment Variable       | Description                                               |
| -------------------- | -------------------------- | --------------------------------------------------------- |
| `--metrics`          | `SLSKD_METRICS`            | Determines whether the metrics endpoint should be enabled |
| `--metrics-url`      | `SLSKD_METRICS_URL`        | The URL of the metrics endpoint                           |
| `--metrics-no-auth`  | `SLSKD_METRICS_NO_AUTH`    | Disables authentication for the metrics endpoint          |
| `--metrics-username` | `SLSKD_METRICS_USERNAME`   | The username for the metrics endpoint                     |
| `--metrics-password` | `SLSKD_METRICS_PASSWORD`   | The password for the metrics endpoint                     |

#### **YAML**

```yaml
metrics:
  enabled: false
  url: /metrics
  authentication:
    disabled: false
    username: slskd
    password: slskd
```

## Features

Several features have been added that aid in the application's development, debugging, and operation but are generally not valuable for most users.

The application can publish Prometheus metrics to `/metrics` using [prometheus-net](https://github.com/prometheus-net/prometheus-net).  This is especially useful for anyone attempting to tune performance characteristics.

The application can publish a Swagger (OpenAPI) definition and host SwaggerUI at `/swagger` using [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore).  This is useful for anyone developing against the application API and/or creating a new web interface.

| Command-Line   | Environment Variable       | Description                                                                               |
| -------------- | -------------------------- | ----------------------------------------------------------------------------------------- |
| `--swagger`    | `SLSKD_SWAGGER`            | Determines whether Swagger (OpenAPI) definitions and UI should be available at `/swagger` |

#### **YAML**
```yaml
feature:
  swagger: false
```

## Development Flags

Several additional feature flags are provided to change the application's runtime behavior, which is helpful during development. Available feature flags are:

| Flag                             | Environment Variable                 | Description                                                      |
| -------------------------------- | ------------------------------------ | ---------------------------------------------------------------- |
| `-d\|--debug`                    | `SLSKD_DEBUG`                        | Run the application in debug mode.  Produces verbose log output. |
| `--experimental`                 | `SLSKD_EXPERIMENTAL`                 | Run the application in experimental mode.  YMMV.                 |
| `-n\|--no-logo`                  | `SLSKD_NO_LOGO`                      | Don't show the application logo on startup                       |
| `-x\|--no-start`                 | `SLSKD_NO_START`                     | Bootstrap the application, but don't start                       |
| `--no-config-watch`              | `SLSKD_NO_CONFIG_WATCH`              | Don't watch the configuration file for changes                   |
| `--no-connect`                   | `SLSKD_NO_CONNECT`                   | Start the application, but don't connect to the server           |
| `--no-share-scan`                | `SLSKD_NO_SHARE_SCAN`                | Don't perform a scan of shared directories on startup            |
| `--force-share-scan`             | `SLSKD_FORCE_SHARE_SCAN`             | Force a scan of shared directories on startup                    |
| `--force-migrations`             | `SLSKD_FORCE_MIGRATIONS`             | Force database migrations to be applied on startup               |
| `--no-version-check`             | `SLSKD_NO_VERSION_CHECK`             | Don't perform a version check on startup                         |
| `--log-sql`                      | `SLSKD_LOG_SQL`                      | Log SQL queries generated by Entity Framework                    |
| `--volatile`                     | `SLSKD_VOLATILE`                     | Use volatile data storage (all data will be lost at shutdown)    |
| `--case-sensitive-regex`         | `SLSKD_CASE_SENSITIVE_REGEX`         | User-defined regular expressions are case sensitive              |
| `--legacy-windows-tcp-keepalive` | `SLSKD_LEGACY_WINDOWS_TCP_KEEPALIVE` | Use a legacy TCP keepalive strategy for older Windows versions   |

#### **YAML**
```yaml
debug: false
flags:
  no_logo: false
  no_start: false
  no_config_watch: false
  no_connect: false
  no_share_scan: false
  force_share_scan: false
  force_migrations: false
  no_version_check: false
  log_sql: false
  experimental: false
  volatile: false
  case_sensitive_reg_ex: false
  legacy_windows_tcp_keepalive: false
```

# Commands

The application can be run in "command mode", causing it to execute a command and quit immediately. Available commands are:

| Command                          | Description                                         |
| -------------------------------- | --------------------------------------------------- |
| `-v\|--version`                  | Display the current application version             |
| `-h\|--help`                     | Display available command-line arguments            |
| `-e\|--envars`                   | Display available environment variables             |
| `-g\|--generate-cert`            | Generate an X509 certificate and password for HTTPS |
| `-k\|--generate-secret <length>` | Generate a random secret of the specified length    |
