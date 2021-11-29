# Feature Flags

A number of feature flags are provided to change the runtime behavior of the application.  Most of these options are useful only for development.  Available feature flags are:

|Flag|Description|
|----|-----------|
|`-d\|--debug`|Run the application in debug mode.  Produces verbose log output.|
|`-n\|--no-logo`|Don't show the application logo on startup|
|`-x\|--no-start`|Bootstrap the application, but don't start|
|`--no-connect`|Start the application, but don't connect to the server|
|`--no-share-scan`|Don't perform a scan of shared directories on startup|
|`--no-version-check`|Don't perform a version check on startup|

# Commands

The application can be run in "command mode", causing it to execute a command and then quit immediately.  Available commands are:

|Command|Description|
|-------|-----------|
|`-v\|--version`|Display the current application version|
|`-h\|--help`|Display available command-line arguments|
|`-e\|--envars`|Display available environment variables|
|`-g\|--generate-cert`|Generate an X509 certificate and password for HTTPS|