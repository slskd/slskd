# slskd
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fslskd%2Fslskd.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fslskd%2Fslskd?ref=badge_shield)


slskd is a modern client-server application for the Soulseek file sharing network.

## Quick Start

```
docker run -d \
  -p 5000:5000 \
  -p 5001:5001 \
  -v <path/to/downloads>:/var/slskd/downloads \
  -v <path/to/shared>:/var/slskd/shared \
  -e "SLSKD_SLSK_USERNAME=<Soulseek username>" \
  -e "SLSKD_SLSK_PASSWORD=<Soulseek password>" \
  --name slskd \
  slskd/slskd:latest
```

This command starts a container instance of slskd on ports 5000 (http) and 5001 (https), maps shared and downloads directories to the paths you choose, and logs in to Soulseek using the credentials provided.

The default credentials for the web UI are slskd/slskd.

## Installation

The latest stable binaries can be downloaded from the [releases](https://github.com/slskd/slskd/releases) page.  Platform specific binaries, along with the static content for the web UI, are produced as artifacts from every [build](https://github.com/slskd/slskd/actions?query=workflow%3ACI), if you'd prefer to use a canary release.

Binaries are shipped as zip files; extract the zip to a directory of your choosing and run.

## Configuration

Configuration is provided via environment variables, a yaml configuration file, and via command line arguments, with the latter source taking precedence if more than one source is used (eg. a value specified in the configuration file overrides the same value specified by an environment variable).

A full list of the environment variables and command line arguments supported by the application can be obtained with the following command:

```
./slskd --help --envars
```

An example of the yaml configuration file can be reviewed [here](https://github.com/slskd/slskd/blob/master/src/slskd/slskd.yml).


## License
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fslskd%2Fslskd.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fslskd%2Fslskd?ref=badge_large)