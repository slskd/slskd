# Configuration 

The application is highly configurable while coming out of the box with sensible defaults.

The defaults that have been chosen should cover the vast majority of users. Still, deployments on low spec hardware (such as a single-board computer or a shared system) should monitor resource usage and adjust as necessary.

Credentials (username and password) for the Soulseek network are the only required configuration, but it is advised to also change the credentials for the Web UI.

# Sources

The application supports several different configuration sources to make it easy for users to deploy the application in various situations.

The configuration options used by the application derive from the values specified by each of the sources, with sources higher in the hierarchy overwriting or expanding the configuration set earlier. The hierarchy is:

```
Default Values < Environment Variables < YAML Configuraiton File < Command Line Arguments
```

Some options can be specified as lists. In these cases, when combining configurations from multiple sources, care should be taken. .NET framework not only appends but also overwrites values. If a list of `one;two;three` is defined in an environment variable for an option, and the YAML configuration specifies a list of `foo` and `bar`, the resulting configuration will be a list containing `foo, bar, three`.

## Default Values

Default values for each option (unless the default is `null`) are specified in `Options.cs`.  Upon startup, these values are copied into the configuration first.

## Environment Variables

Environment variables are loaded after defaults. Each environment variable name is prefixed with `SLSKD_` (this prefix is omitted for the remainder of this documentation).

Environment variables are ideal for use cases involving Docker where configuration is expected to be changed neither often nor remotely.

Some options are backed by arrays or lists and allow multiple options to be set via a single environment variable. To achieve this, separate each list item by a semicolon `;` in the value:

```
SLSKD_SOME_OPTION=1;2;3
```

## YAML Configuration File

Configuration is loaded from the YAML file located at `<application directory>/slskd.yml` after environment variables. The location can be changed by specifying the path in the `CONFIG` environment variable or `--config` command-line argument.

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

| Command-Line             | Environment Variable   | Description                                                   |
| ------------------------ | ---------------------- | ------------------------------------------------------------- |
| `--remote-configuration` | `REMOTE_CONFIGURATION` | Determines whether the remote configuration of options is allowed |

#### **YAML**
```yaml
remote_configuration: false
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

| Command-Line      | Environment Variable | Description                                   |
| ----------------- | -------------------- | --------------------------------------------- |
| `-o\|--downloads` | `DOWNLOADS_DIR`      | The path where downloaded files are saved     |
| `--incomplete`    | `INCOMPLETE_DIR`     | The path where incomplete downloads are saved |

#### **YAML**
```yaml
directories:
  incomplete: ~
  downloads: ~
```

## Shares

Any number of shared directories can be configured.

Paths must be absolute, meaning they must begin with `/`, `X:\`, or `\\`, depending on the system. Relative paths, such as `~/directory` or `../directory`, are not supported. Sharing a root mount on a unix-like OS (`/`) is also not supported.

Shares can be excluded by prefixing them with `-` or `!`. This is useful in situations where sharing a subdirectory of a share isn't desired, for example, if a user wants to share their entire music library but not their personal recordings:

```yaml
directories:
  shared:
    - 'D:\Music'
    - '!D:\Music\Personal Recordings`
```

Shares can be aliased to improve privacy (for example, if a username is present in the path). A share alias can be specified by prefixing the share with the alias in square brackets, for example:

```yaml
directories:
  shared:
    - '[Music]\users\John Doe\Music'
```

If no alias is specified, the name of the shared folder is used (e.g. `D:\Music` is aliased to `Music`). To a remote user, any files within the aliased path will appear as though they are shared from a directory named as the alias.

Aliases:
* Must be unique
* Must be at least one character in length
* Must not contain path separators (`\` or `/`)

| Command-Line   | Environment Variable | Description                       |
| -------------- | -------------------- | --------------------------------- |
| `-s\|--shared` | `SHARED_DIR`         | The list of paths to shared files |

#### **YAML**
```yaml
directories:
  shared:
    - ~
```

# Limits and User Groups

## Global

Global limits behave as a hard limit, additive across all groups. These values should be set as high as practical for the application's environment; more granular controls should be defined at the group level.

A change to slot limits requires an application restart to take effect, while speed limits can be adjusted at runtime.

| Command-Line             | Environment Variable   | Description                                      |
| ------------------------ | ---------------------- | ------------------------------------------------ |
| `--upload-slots`         | `UPLOAD_SLOTS`         | The limit for the total number of upload slots   |
| `--upload-speed-limit`   | `UPLOAD_SPEED_LIMIT`   | The total upload speed limit                     |
| `--download-slots`       | `DOWNLOAD_SLOTS`       | The limit for the total number of download slots |
| `--download-speed-limit` | `DOWNLOAD_SPEED_LIMIT` | The total download speed limit                   |

#### **YAML**
```yaml
global:
  upload:
    slots: 20
    speed_limit: 1000
  download:
    slots: 500
    speed_limit: 1000
```

## Groups

User groups are used to control upload slots, speed limits, and queue behavior on a per-user basis.

Each group has a priority, starting from 1, determining the order in which groups are prioritized in the upload queue. A lower number translates to higher priority, and Upload slots are granted to higher priority groups before lower priority groups.

Groups have a queue strategy, either `FirstInFirstOut` or `RoundRobin`. This setting determines how uploads from multiple users in the same group are processed; `FirstInFirstOut` processes uploads in the order in which they were enqueued, while `RoundRobin` processes uploads in the order in which the user was ready to receive the upload.

Upload slots and speed limits configured at the group level can be used to create constraints in addition to global settings. If group-level limits exceed global settings, global settings become the constraint. Slots and speed limit settings default to `int.MaxValue`, effectively deferring to the global limits.

The general configuration for a group is as follows:

#### **YAML**
```yaml
upload:
  priority: 500 # 1 to int.MaxValue
  strategy: roundrobin # roundrobin or firstinfirstout
  slots: 10 # 1 to int.MaxValue
  speed_limit: 100 # in KiB/s, 1 to int.MaxValue
```

## Built-In Groups

The `default` built-in group contains all users who have not been explicitly added to a user-defined group, are not privileged, and haven't been identified as leechers.

The `leechers` built-in group contains users that have not been explicitly added to a user-defined group, are not privileged, and have shared file and/or directory counts less than the configured `thresholds` for the group. By default, users must share at least one directory with one file to avoid being identified as leechers.

The `privileged` built-in is used to prioritize users who have purchased privileges on the Soulseek network. This groups is not configurable, has a priority of 0 (the highest), a strategy of `FirstInFirstOut`, and can use any number of slots up to the global limit.

It is impossible to explicitly assign users to built-in groups, but the priority, number of slots, speed, and queue strategy can be adjusted (excluding `privileged`).

#### **YAML**
```yaml
groups:
  default:
    upload:
      priority: 1
      strategy: roundrobin
      slots: 10
      speed_limit: 50000
  leechers:
    thresholds:
      files: 1
      directories: 1
    upload:
      priority: 99
      strategy: roundrobin
      slots: 1
      speed_limit: 100
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

# Soulseek Configuration

The Soulseek configuration determines how slskd interacts with the Soulseek network and underlying [Soulseek.NET](https://github.com/jpdillingham/Soulseek.NET) library.

## Username and Password

Credentials to log in to the Soulseek network.

Changing either of these values requires the server connection to be reset.

The password field is masked when serializing options to JSON or YAML to avoid inadvertently exposing the value.

| Command-Line      | Environment Variable | Description                           |
| ----------------- | -------------------- | ------------------------------------- |
| `--slsk-username` | `SLSK_USERNAME`      | The username for the Soulseek network |
| `--slsk-password` | `SLSK_PASSWORD`      | The password for the Soulseek network |

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
| `--slsk-no-dnet`          | `SLSK_NO_DNET`          | Determines whether the distributed network is disabled. If disabled, the client will not obtain a parent or any child connections, and will not receive distributed search requests. |
| `--slsk-dnet-no-children` | `SLSK_DNET_NO_CHILDREN` | Determines whether to disallow distributed children                                                                                                                                   |
| `--slsk-dnet-children`    | `SLSK_DNET_CHILDREN`    | The maximum number of distributed children to accept                                                                                                                                  |
| `--slsk-dnet-logging`     | `SLSK_DNET_LOGGING`     | Determines whether to enable distributed network logging

#### **YAML**
```yaml
soulseek:
  distributed_network:
    disabled: false
    disable_children: false
    child_limit: 25
```

## Listen Port

The port on which the application listens for incoming connections.

As with any other Soulseek client, configuring the listen port and port forwarding ensures full connectivity with other clients, including those without a correctly configured a listening port.  

Symptoms of a misconfigured listen port include poor search results and the inability to browse or retrieve user information for some users.

| Command-Line         | Environment Variable | Description                                          |
| -------------------- | -------------------- | ---------------------------------------------------- |
| `--slsk-listen-port` | `SLSK_LISTEN_PORT`   | The port on which to listen for incoming connections |

#### **YAML**
```yaml
soulseek:
  listen_port: 50000
```

## Other

| Command-Line         | Environment Variable | Description                                   |
| -------------------- | -------------------- | --------------------------------------------- |
| `--slsk-description` | `SLSK_DESCRIPTION`   | The user description for the Soulseek network |

#### **YAML**
```yaml
soulseek:
  description: A slskd user. https://github.com/slskd/slskd
```

## Connection Options

### Timeouts

Timeout options control how long the application waits for connections to connect, and how long connections can be inactive before they are disconnected.

Higher connect timeout values will help ensure that operations (browse, download requests, etc.) are successful the first time but decrease the responsiveness of commands that will ultimately fail.

Inactivity timeouts help the application determine when a distributed parent connection has stopped sending data and when connections that have delivered search results (and are unlikely to be used further) from remaining open longer than needed. Reducing this timeout can help low spec systems if port exhaustion is a concern but may result in the application "hunting" for a distributed parent connection needlessly.

| Command-Line                | Environment Variable      | Description                                      |
| --------------------------- | ------------------------- | ------------------------------------------------ |
| `--slsk-connection-timeout` | `SLSK_CONNECTION_TIMEOUT` | The connection timeout value, in milliseconds    |
| `--slsk-inactivity-timeout` | `SLSK_INACTIVITY_TIMEOUT` | The connection inactivity value, in milliseconds |

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

| Command-Line             | Environment Variable   | Description                                        |
| ------------------------ | ---------------------- | -------------------------------------------------- |
| `--slsk-read-buffer`     | `SLSK_READ_BUFFER`     | The connection read buffer size, in bytes          |
| `--slsk-write-buffer`    | `SLSK_WRITE_BUFFER`    | The connection write buffer size, in bytes         |
| `--slsk-transfer-buffer` | `SLSK_TRANSFER_BUFFER` | The read/write buffer size for transfers, in bytes |
| `--slsk-write-queue`     | `SLSK_WRITE_QUEUE`     | The connection write buffer size, in bytes         |

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

| Command-Line            | Environment Variable  | Description                              |
| ----------------------- | --------------------- | ---------------------------------------- |
| `--slsk-proxy`          | `SLSK_PROXY`          | Determines whether a proxy is to be used |
| `--slsk-proxy-address`  | `SLSK_PROXY_ADDRESS`  | The proxy address                        |
| `--slsk-proxy-port`     | `SLSK_PROXY_PORT`     | The proxy port                           |
| `--slsk-proxy-username` | `SLSK_PROXY_USERNAME` | The proxy username, if applicable        |
| `--slsk-proxy-password` | `SLSK_PROXY_PASSWORD` | The proxy password, if applicable        |

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

| Command-Line        | Environment Variable | Description                                               |
| ------------------- | -------------------- | --------------------------------------------------------- |
| `--slsk-diag-level` | `SLSK_DIAG_LEVEL`    | The minimum diagnostic level (None, Warning, Info, Debug) |

#### **YAML**
```yaml
soulseek:
  diagnostic_level: Info
```

# Web Configuration

## Basic

The default HTTP listen port is 5000, typical for a .NET application, but can be anything between 1 and 65535.

The URL base option allows the application to operate behind a reverse proxy. Setting a base of "slskd" would make the web UI accessible at `http://<host>:<port>/slskd`.

The content path can be used to force the application to serve static web content from a location other than the default (`wwwroot`). The application is designed to decouple the Web UI from the rest of the application to be replaceable.

Logging of HTTP requests is disabled by default.

| Command-Line      | Environment Variable | Description                                       |
| ----------------- | -------------------- | ------------------------------------------------- |
| `-l\|--http-port` | `HTTP_PORT`          | The HTTP listen port                              |
| `--url-base`      | `URL_BASE`           | The base url for web requests                     |
| `--content-path`  | `CONTENT_PATH`       | The path to static web content                    |
| `--http-logging`  | `HTTP_LOGGING`       | Determines whether HTTP requests are to be logged |

#### **YAML**
```yaml
web:
  port: 5000
  url_base: /
  content_path: wwwroot
  logging: false
```

## HTTPS

The default HTTPS port is 5001, typical for a .NET application but can be anything between 1 and 65535.

By default, the application generates a new, self-signed X509 certificate at each startup. If, for whatever reason, a self-signed certificate isn't sufficient, or if the certificate needs to be shared among systems or applications, a certificate `.pfx` and password can be defined.

The application can produce a self-signed `.pfx` file and random password using the `--generate-cert` command.

| Command-Line            | Environment Variable  | Description                                                    |
| ----------------------- | --------------------- | -------------------------------------------------------------- |
| `-L\|--https-port`      | `HTTPS_PORT`          | The HTTPS listen port                                          |
| `-f\|--force-https`     | `HTTPS_FORCE`         | Determines whether HTTP requests are to be redirected to HTTPS |
| `--https-cert-pfx`      | `HTTPS_CERT_PFX`      | The path to the X509 certificate .pfx file                     |
| `--https-cert-password` | `HTTPS_CERT_PASSWORD` | The password for the X509 certificate                          |
| `--no-https`            | `NO_HTTPS`            | Determines whether HTTPS is to be disabled                     |

#### **YAML**
```yaml
web:
  https:
    port: 5001
    force: false
    certificate:
      pfx: ~
      password: ~
```

## Authentication

Authentication for the web UI (and underlying API) is enabled by default, and the default username and password are both `slskd`. Changing both the username and password during the initial configuration is highly recommended.

By default, a random JWT secret key is generated at each start. It is convenient and secure, but restarting the application will invalidate any issued JWTs, causing users to sign in again. To avoid this, supply a custom secret at least 16 characters in length. Note that the secret can be used to generate valid JWTs for the application, so keep this value secret.

The JWT TTL option determines how long issued JWTs are valid, defaulting to 7 days.

| Command-Line     | Environment Variable | Description                                         |
| ---------------- | -------------------- | --------------------------------------------------- |
| `-X\|--no-auth`  | `NO_AUTH`            | Determines whether authentication is to be disabled |
| `-u\|--username` | `USERNAME`           | The username for the web UI                         |
| `-p\|--password` | `PASSWORD`           | The password for the web UI                         |
| `--jwt-key`      | `JWT_KEY`            | The secret key used to sign JWTs                    |
| `--jwt-ttl`      | `JWT_TTL`            | The TTL (duration) of JWTs, in milliseconds         |

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
```

# Filters

A number of filters can be configured to control various aspects of how the application interacts with the Soulseek network.

Share filters can be used to prevent certain types of files from being shared.  This option is an array that can take any number of filters.  Filters must be a valid regular expression; a few examples are included below and in the example configuration included with the application, but the list is empty by default.

Search request filters can be used to discard incoming search requests that match one or more filters.  Like share filters, this option is an array of regular expressions, and it defaults to an empty array (no request filtering is applied).

| Command Line              | Environment Variable    | Description                                                           |
| ------------------------- | ----------------------- | --------------------------------------------------------------------- |
| `--share-filter`          | `SHARE_FILTER`          | A list of regular expressions used to filter files from shares        |
| `--search-request-filter` | `SEARCH_REQUEST_FILTER` | A list of regular expressions used to filter incoming search requests |

#### **YAML**
```yaml
filters:
  share:
    - \.ini$
    - Thumbs.db$
    - \.DS_Store$
  search:
    request:
      - ^.{1,2}$
```

# Integrations

## FTP

Files can be uploaded to a remote FTP server upon completion. Files are uploaded to the server and remote path specified using the directory and filename with which they were downloaded; the FTP will match the layout of the local disk.

Uploads are attempted up to the maximum configured retry count and then discarded.

| Command-Line                      | Environment Variable            | Description                                              |
| --------------------------------- | ------------------------------- | -------------------------------------------------------- |
| `--ftp`                           | `FTP`                           | Determines whether FTP integration is enabled            |
| `--ftp-address`                   | `FTP_ADDRESS`                   | The FTP address                                          |
| `--ftp-port`                      | `FTP_PORT`                      | The FTP port                                             |
| `--ftp-username`                  | `FTP_USERNAME`                  | The FTP username                                         |
| `--ftp-password`                  | `FTP_PASSWORD`                  | The FTP password                                         |
| `--ftp-remote-path`               | `FTP_REMOTE_PATH`               | The remote path for uploads                              |
| `--ftp-encryption-mode`           | `FTP_ENCRYPTION_MODE`           | The FTP encryption mode (none, implicit, explicit, auto) |
| `--ftp-ignore-certificate-errors` | `FTP_IGNORE_CERTIFICATE_ERRORS` | Determines whether to ignore FTP certificate errors      |
| `--ftp-overwrite-existing`        | `FTP_OVERWRITE_EXISTING`        | Determines whether existing files should be overwritten  |
| `--ftp-connection-timeout`        | `FTP_CONNECTION_TIMEOUT`        | The connection timeout value, in milliseconds            |
| `--ftp-retry-attempts`            | `FTP_RETRY_ATTEMPTS`            | The number of times failing uploads will be retried      |

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

## Pushbullet

Pushbullet notifications can be sent when a private message is sent, or the current user's username is mentioned in a chat room. Notifications are prefixed with a user-definable string to differentiate these notifications from others.  

A Pushbullet account must be created, and users must create an API key within the Pushbullet application and configure it through options. Complete documentation for the Pushbullet API, including the latest instructions for obtaining an API key or "Access Token" can be found [here](https://docs.pushbullet.com/).

The Pushbullet integration is one-way, meaning the application cannot know whether a user is active or receiving notifications. To prevent an inappropriate number of notifications from being sent, for example, if a user is carrying on an active conversation, a "cooldown" option is provided to ensure that notifications are sent only after the cooldown has expired. By default, this is every 15 minutes.

Notification API calls are made up to the maximum configured retry count and then discarded.

| Command-Line                          | Environment Variable                   | Description                                                                                        |
| ------------------------------------- | -------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `--pushbullet`                        | `PUSHBULLET`                           | Determines whether Pushbullet integration is enabled                                               |
| `--pushbullet-token`                  | `PUSHBULLET_TOKEN`                     | The Pushbullet API access token                                                                    |
| `--pushbullet-prefix`                 | `PUSHBULLET_PREFIX`                    | The prefix for notification titles                                                                 |
| `--pushbullet-notify-on-pm`           | `PUSHBULLET_NOTIFY_ON_PRIVATE_MESSAGE` | Determines whether to send a notification when a private message is received                       |
| `--pushbullet-notify-on-room-mention` | `PUSHBULLET_NOTIFY_ON_ROOM_MENTION`    | Determines whether to send a notification when the current user's name is mentioned in a chat room |
| `--pushbullet-retry-attempts`         | `PUSHBULLET_RETRY_ATTEMPTS`            | The number of times failing API calls will be retried                                              |
| `--pushbullet-cooldown`               | `PUSHBULLET_COOLDOWN_TIME`             | The cooldown time for notifications, in milliseconds                                               |

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

| Command-Line          | Environment Variable | Description                              |
| --------------------- | -------------------- | ---------------------------------------- |
| `-i\|--instance-name` | `INSTANCE_NAME`      | The unique name for the running instance |

#### **YAML**
```yaml
instance_name: default
```

## Loggers

By default, the application logs to disk (`/logs` in the application directory). Logs can optionally be forwarded to external services, and the targets can be expanded to any service supported by a [Serilog Sink](https://github.com/serilog/serilog/wiki/Provided-Sinks). Support for targets is added on an as-needed basis and within reason.

The current list of available targets is:

| Command Line | Environment Variable | Description                        |
| ------------ | -------------------- | ---------------------------------- |
| `--loki`     | `LOKI`               | The URL to a Grafana Loki instance |

#### **YAML**
```yaml
logger:
  loki: ~
```

## Metrics

The application captures metrics internally and can optionally expose these metrics to be consumed by an instance of Prometheus.  This is a good option for those wanting to tune performance characterisics of the application.

Metrics are disabled by default, and enabling them will make them available at `/metrics`.  Authentication is enabled by default, and the credentials are the same defaults as the web UI (`slskd`:`slskd`).

If the application will be exposed to the internet, it's a good idea to leave this disabled or to set credentials other than the defaults.  Elements of the system configuration, like operating system, architecture, and drive configuration are included and can this can make it easier for an attacker to exploit a vulnerability in your system.


| Command Line         | Environment Variable | Description                                               |
| -------------------- | -------------------- | --------------------------------------------------------- |
| `--metrics`          | `METRICS`            | Determines whether the metrics endpoint should be enabled |
| `--metrics-url`      | `METRICS_URL`        | The URL of the metrics endpoint                           |
| `--metrics-no-auth`  | `METRICS_NO_AUTH`    | Disables authentication for the metrics endpoint          |
| `--metrics-username` | `METRICS_USERNAME`   | The username for the metrics endpoint                     |
| `--metrics-password` | `METRICS_PASSWORD`   | The password for the metrics endpoint                     |

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

| Command-Line   | Environment Variable | Description                                                                               |
| -------------- | -------------------- | ----------------------------------------------------------------------------------------- |
| `--swagger`    | `SWAGGER`            | Determines whether Swagger (OpenAPI) definitions and UI should be available at `/swagger` |

#### **YAML**
```yaml
feature:
  swagger: false
```

## Development Flags

Several additional feature flags are provided to change the application's runtime behavior, which is helpful during development. Available feature flags are:

| Flag                 | Environment Variable | Description                                                      |
| -------------------- | -------------------- | ---------------------------------------------------------------- |
| `-d\|--debug`        | `DEBUG`              | Run the application in debug mode.  Produces verbose log output. |
| `--experimental`     | `EXPERIMENTAL`       | Run the application in experimental mode.  YMMV. |
| `-n\|--no-logo`      | `NO_LOGO`            | Don't show the application logo on startup                       |
| `-x\|--no-start`     | `NO_START`           | Bootstrap the application, but don't start                       |
| `--no-connect`       | `NO_CONNECT`         | Start the application, but don't connect to the server           |
| `--no-share-scan`    | `NO_SHARE_SCAN`      | Don't perform a scan of shared directories on startup            |
| `--no-version-check` | `NO_VERSION_CHECK`   | Don't perform a version check on startup                         |
| `--log-sql`          | `LOG_SQL`            | Log SQL queries generated by Entity Framework                    |

#### **YAML**
```yaml
debug: false
flags:
  no_logo: false
  no_start: false
  no_connect: false
  no_share_scan: false
  no_version_check: false
  log_sql: false
```

# Commands

The application can be run in "command mode", causing it to execute a command and quit immediately. Available commands are:

| Command               | Description                                         |
| --------------------- | --------------------------------------------------- |
| `-v\|--version`       | Display the current application version             |
| `-h\|--help`          | Display available command-line arguments            |
| `-e\|--envars`        | Display available environment variables             |
| `-g\|--generate-cert` | Generate an X509 certificate and password for HTTPS |
