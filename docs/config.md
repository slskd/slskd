# Sources

slskd supports several different configuration sources in order to make it easy for users to deploy the application in a variety of situations.

The configuration options used by the application are derived from the values specified by each of the sources, with sources higher in the heirarchy overwriting (or sometimes adding to) configuration specified lower in the heirarchy.  The heirarchy is:

```
Default Values < Environment Variables < YAML Configuraiton File < Command Line Arguments
```

The defaults that have been chosen should cover the vast majority of users, but deployments on low spec hardware (such as a single board computer, or a shared system) should monitor resource usage and adjust as necessary.  Memory usage is the key metric.

Credentials (username and password) for the Soulseek network are the only required configuration.

## Default Values

Default values for each option (unless the default is `null`) are specified in `Options.cs`.  Upon startup these values are copied into the configuration first.

## Environment Variables

Environment variables are loaded after defaults.  Each environment variable name is prefixed with `SLSKD_` (this prefix is omitted for the remainder of this documentation).

Environment variables are ideal for use cases involving Docker where remote configuration will be disabled, and where configuration is not expected to change often.

Some options are backed by arrays or lists and allow multiple options to be set via a single environment variable.  To achieve this, separate each list item by a semicolon `;` in the value:

```
SLSKD_SOME_OPTION=1;2;3
```

## YAML Configuration File

Configuration is loaded from the YAML file located at `<application directory>/slskd.yml` after environment variables.

The application watches for changes in the YAML file and will reload configuration when they are detected.  Options will be updated in real time and transmitted to the web UI.  If a server reconnect or application restart is required for changes to fully take effect, a flag will be set indicating so.

The YAML file can be read and written at run time via API calls, or can be edited on disk.

If no such configuraiton file exists at startup, the example file `/config/slskd.example.yml` is copied to this location for convenience.

YAML configuration should be used in most cases.

## Command Line Arguments

Command line arguments are loaded last, override all other configuration options, and are immutable at run time.

Command line arguments are useful for security options, such as remote configuration, HTTPS JWT secret and certificate.  Choosing this source for sensitive options can prevent a remote attacker from gaining full control over the application.

Some options are backed by arrays or lists and allow multiple options to be set via command line.  To achieve this, repeat the command line argument:

```
--some-option 1 --some-option 2 --some-option 3
```

# Application Directory Configuration

The application directory configuration option determines the location of the YAML file, the default locations of the download and incomplete directories, and the location of application working data, such as logs and SQLite databases.

Because the location of the YAML file is derived from this value, it can't be specified within the YAML file and must instead be specified with the `APP_DIR` environment variable or `--app-dir` command line argument.

If no value is specified, the location defaults to either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows).

Within the official Docker image, this value is set to `/app`.

# Other Directory Configuration

## Incomplete and Downloads

## Shares

# Soulseek Configuration

The Soulseek configuration determines how slskd will interact with the Soulseek network and underlying [Soulseek.NET](https://github.com/jpdillingham/Soulseek.NET) library.

## Username and Password

The credentials used to log in to the Soulseek network.

Changing either of these values requires the server connection to be reset.

The password field is not included when serializing options to Json or YAML to avoid inadvertently exposing the value.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-username`|`SLSK_USERNAME`|The username for the Soulseek network|
|`--slsk-password`|`SLSK_PASSWORD`|The password for the Soulseek network|

#### **YAML**
```yaml
soulseek:
  username: <username>
  password: <password>
```

## Distributed Network

Options for the Soulseek distributed network, which is how search requests are delivered.

The distributed network should only be disabled if no files are being shared.  

Child connections should generally only be disabled on low spec systems or situations where network bandwidth is scarce.  Received search requests are re-broadcast to each child connection, and incoming requests are numerous.  Consider increasing the child limit from the default of 25 on systems with CPU and memory headroom.

Changing these values may require the server connection to be reset, depending on the current state.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-no-dnet`|`SLSK_NO_DNET`|Determines whether the distributed network is disabled.  If disabled, the client will not obtain a parent or any child connections, and will not receive distributed search requests.|
|`--slsk-dnet-no-children`|`SLSK_DNET_NO_CHILDREN`|Determines whether to disallow distributed children|
|`--slsk-dnet-children`|`SLSK_DNET_CHILDREN`|The maximum number of distributed children to accept|

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

As with any other Soulseek client, properly configuring the listen port and port forwarding ensures full connectivity with other clients, including those who have not properly configured a listening port.  

Symptoms of a misconfigured listen port include poor search results and inability to browse or retrieve user information for some users.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-listen-port`|`SLSK_LISTEN_PORT`|The port on which to listen for incoming connections|

#### **YAML**
```yaml
soulseek:
  listen_port: 50000
```

## Connection

### Timeouts

Timeout options control how long the application waits for connections to connect, and how long connections can be inactive before they are disconnected.

Higher connect timeout values will help ensure that operations (browse, download requests, etc) are successful the first time, but also decrease responsiveness of commands that will ultimately fail.

Inactivity timeouts help the application determine when a distributed parent connection has stopped sending data, and when connections that have delivered search results (and are unlikely to be used further) from remaining open longer than needed.  Reducing this timeout can help on low spec systems if port exhaustion is a concern, but may result in the application "hunting" for a distributed parent connection needlessly.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-connection-timeout`|`SLSK_CONNECTION_TIMEOUT`|The connection timeout value, in milliseconds|
|`--slsk-inactivity-timeout`|`SLSK_INACTIVITY_TIMEOUT`|The connection inactivity value, in milliseconds|

#### **YAML**
```yaml
soulseek:
  connection:
    timeout:
      connect: 10000
      inactivity: 15000  
```

### Buffers

Buffer options control the internal buffer the application uses to batch socket reads and writes.  Actual socket buffer sizes are controlled by the OS (.NET is not great in this department currently, and the behavior of these is not consistent cross-platform).

Larger buffer sizes can improve performance, especially for file transfers, but result in increased memory usage.

The transfer buffer size is used for file transfers (both read and write), and the read and write buffer sizes are used for all other connection types.  The transfer buffer size is directly correlated with transfer speed; setting this value much lower than the default will result in slow uploads (which may be a good thing for low spec hardware).

The write queue option is the hard limit for the number of concurrent writes for a connection.  This generally only applies to distributed child connections, and prevents a memory leak if the application continues to try and send data after a connection has gone "bad".  This value can be set as low as 5 if memory is a constraint, though the default has been tested extensively and should be good for most scenarios, including low spec.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-read-buffer`|`SLSK_READ_BUFFER`|The connection read buffer size, in bytes|
|`--slsk-write-buffer`|`SLSK_WRITE_BUFFER`|The connection write buffer size, in bytes|
|`--slsk-transfer-buffer`|`SLSK_TRANSFER_BUFFER`|The read/write buffer size for transfers, in bytes|
|`--slsk-write-queue`|`SLSK_WRITE_QUEUE`|The connection write buffer size, in bytes|

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

If the proxy is enabled, an address and port must also be specified.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-proxy`|`SLSK_PROXY`|Determines whether a proxy is to be used|
|`--slsk-proxy-address`|`SLSK_PROXY_ADDRESS`|The proxy address|
|`--slsk-proxy-port`|`SLSK_PROXY_PORT`|The proxy port|
|`--slsk-proxy-username`|`SLSK_PROXY_USERNAME`|The proxy username, if applicable|
|`--slsk-proxy-password`|`SLSK_PROXY_PASSWORD`|The proxy password, if applicable|

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

The diagnostic level option is passed to the Soulseek.NET configuration, and determines the level of detail at which the library produces diagnostic messages.  This should generally be left set to `Info` or `Warning`, but can be set to `Debug` if more verbose logging is desired.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--slsk-diag-level`|`SLSK_DIAG_LEVEL`|The minimum diagnostic level (None, Warning, Info, Debug)|

#### **YAML**
```yaml
soulseek:
  diagnostic_level: Info
```

# Web Configuration

## Basic

The default HTTP listen port is 5000, typical for a .NET application, but can be anything between 1 and 65535.

The url base option allows the application to operate behind a reverse proxy. Setting a base of "slskd" would make the web UI accessible at `http://<host>:<port>/slskd`.

The content path can be used to force the application to serve static web content from a location other than the default (`wwwroot`).  The application is designed such that the web UI is decoupled from the rest of the application, and can be swapped for another.

Logging of HTTP requests is disabled by default.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`-l\|--http-port`|`HTTP_PORT`|The HTTP listen port|
|`--url-base`|`URL_BASE`|The base url for web requests|
|`--content-path`|`CONTENT_PATH`|The path to static web content|
|`--http-logging`|`HTTP_LOGGING`|Determines whether HTTP requests are to be logged|

#### **YAML**
```yaml
web:
  port: 5000
  url_base: /
  content_path: wwwroot
  logging: false
```

## HTTPS

The default HTTPS port is 5001, which again is typical for a .NET application, but can be anything between 1 and 65535.

By default, the application generates a new, self-signed X509 certificate at each startup.  If for whatever reason a self-signed certificate isn't sufficient, or if the certificate needs to be shared among systems or applications, a certificate `.pfx` and password can be defined.

The application can produce a self-signed `.pfx` file and random password using the `--generate-cert` command.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`-L\|--https-port`|`HTTPS_PORT`|The HTTPS listen port|
|`-f\|--force-https`|`HTTPS_FORCE`|Determines whether HTTP requests are to be redirected to HTTPS|
|`--https-cert-pfx`|`HTTPS_CERT_PFX`|The path to the X509 certificate .pfx file|
|`--https-cert-password`|`HTTPS_CERT_PASSWORD`|The password for the X509 certificate|

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

Authentication for the web UI (and underlying API) is enabled by default, and the default username and password are both `slskd`.  Changing both the username and password during initial configuration are highly recommended.

By default, a random JWT secret key is generated at each start.  This is convenient and secure, but it means that restarting the application will invalidate any issued JWTs, causing users to have to sign in again.  To avoid this supply a custom secret at least 16 characters in length.  Note that the secret can be used to generate valid JWTs for the application, so keep this value secret.

The JWT TTL option determines how long issued JWTs are valid, defaulting to 7 days.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`-X\|--no-auth`|`NO_AUTH`|Determines whether authentication is to be disabled|
|`-u\|--username`|`USERNAME`|The username for the web UI|
|`-p\|--password`|`PASSWORD`|The password for the web UI|
|`--jwt-key`|`JWT_KEY`|The secret key used to sign JWTs|
|`--jwt-ttl`|`JWT_TTL`|The TTL (duration) of JWTs, in milliseconds|

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

The share filters can be used to prevent certain types of files from being shared.  This option is an array that can take any number of filters.  Filters must be a valid regular expression; a few examples are included below and in the example configuration included with the application, but the list is empty by default.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--share-filter`|`SHARE_FILTER`|A list of regular expressions used to filter files from shares|

#### **YAML**
```yaml
filters:
  share:
    - \.ini$
    - Thumbs.db$
    - \.DS_Store$
```

# Integrations

## FTP

Files can be uploaded to a remote FTP server upon completion.  Files are uploaded to the server and remote path specified using the directory and filename with which they were downloaded; the FTP will match the layout of the local disk.

Uploads are attempted up to the maximum configured retry count, and then discarded.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--ftp`|`FTP`|Determines whether FTP integration is enabled|
|`--ftp-address`|`FTP_ADDRESS`|The FTP address|
|`--ftp-port`|`FTP_PORT`|The FTP port|
|`--ftp-username`|`FTP_USERNAME`|The FTP username|
|`--ftp-password`|`FTP_PASSWORD`|The FTP password|
|`--ftp-remote-path`|`FTP_REMOTE_PATH`|The remote path for uploads|
|`--ftp-encryption-mode`|`FTP_ENCRYPTION_MODE`|The FTP encryption mode (none, implicit, explicit, auto)|
|`--ftp-ignore-certificate-errors`|`FTP_IGNORE_CERTIFICATE_ERRORS`|Determines whether to ignore FTP certificate errors|
|`--ftp-overwrite-existing`|`FTP_OVERWRITE_EXISTING`|Determines whether existing files should be overwritten|
|`--ftp-connection-timeout`|`FTP_CONNECTION_TIMEOUT`|The connection timeout value, in milliseconds|
|`--ftp-retry-attempts`|`FTP_RETRY_ATTEMPTS`|The number of times failing uploads will be retried|

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

# Other Configuration

## Instance Name

The instance name uniquely identifies the running instance of the application.  This is primarily useful for structured logging in cases where multiple instances are logging to the same remote source.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`-i\|--instance-name`|`INSTANCE_NAME`|The unique name for the running instance|

#### **YAML**
```yaml
instance_name: default
```

## Loggers

The application logs to disk (`/logs` in the application directory) by default.  Logs can optionally be forwarded to external services, and the targets can be expanded to any service supported by a [Serilog Sink](https://github.com/serilog/serilog/wiki/Provided-Sinks).  Support for targets are added on an as-needed basis, and within reason.

The current list of available targets is:

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--loki`|`LOKI`|The URL to a Grafana Loki instance|

#### **YAML**
```yaml
logger:
  loki: ~
```

## Features

Several features have been added that aid in the development, debugging and operation of the application, but are generally not of much use to most users.

The application can publish Prometheus metrics to `/metrics` using [prometheus-net](https://github.com/prometheus-net/prometheus-net).  This is especially useful for anyone attempting to tune performance characteristics.

The application can publish a Swagger (OpenAPI) definition and host SwaggerUI at `/swagger` using [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore).  This is useful for anyone developing against the application API and/or creating a new web interface.

|Command Line|Environment Variable|Description|
|----|-----|-----------|
|`--prometheus`|`PROMETHEUS`|Determines whether Prometheus metrics are published to `/metrics`|
|`--swagger`|`SWAGGER`|Determines whether Swagger (OpenAPI) definitions and UI should be available at `/swagger`|

#### **YAML**
```yaml
feature:
  prometheus: false
  swagger: false
```

## Development Flags

A number of additional feature flags are provided to change the runtime behavior of the application, which is useful during development.  Available feature flags are:

|Flag|Environment Variable|Description|
|----|-----|-----------|
|`-d\|--debug`|`DEBUG`|Run the application in debug mode.  Produces verbose log output.|
|`-n\|--no-logo`|`NO_LOGO`|Don't show the application logo on startup|
|`-x\|--no-start`|`NO_START`|Bootstrap the application, but don't start|
|`--no-connect`|`NO_CONNECT`|Start the application, but don't connect to the server|
|`--no-share-scan`|`NO_SHARE_SCAN`|Don't perform a scan of shared directories on startup|
|`--no-version-check`|`NO_VERSION_CHECK`|Don't perform a version check on startup|

# Commands

The application can be run in "command mode", causing it to execute a command and then quit immediately.  Available commands are:

|Command|Description|
|-------|-----------|
|`-v\|--version`|Display the current application version|
|`-h\|--help`|Display available command-line arguments|
|`-e\|--envars`|Display available environment variables|
|`-g\|--generate-cert`|Generate an X509 certificate and password for HTTPS|