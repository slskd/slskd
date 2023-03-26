# slskd

[![Build](https://img.shields.io/github/workflow/status/slskd/slskd/CI/master?logo=github)](https://github.com/slskd/slskd/actions/workflows/ci.yml)
[![Docker Pulls](https://img.shields.io/docker/pulls/slskd/slskd?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![GitHub all releases](https://img.shields.io/github/downloads/slskd/slskd/total?logo=github&color=brightgreen)](https://github.com/slskd/slskd/releases)
[![Contributors](https://img.shields.io/github/contributors/slskd/slskd?logo=github)](https://github.com/slskd/slskd/graphs/contributors)
[![Discord](https://img.shields.io/discord/971446666257391616?label=Discord&logo=discord)](https://slskd.org/discord)
[![Matrix](https://img.shields.io/badge/Matrix-%3F%20online-na?logo=matrix&color=brightgreen)](https://slskd.org/matrix)

A modern client-server application for the Soulseek file sharing network.

## Quick Start

### With Docker
```
docker run -d \
  -p 5000:5000 \
  -p 5001:5001 \
  -p 50000:50000 \
  -v <path/to/application/data>:/app \
  --name slskd \
  slskd/slskd:latest
```
### With Docker-Compose

```
---
version: "2"
services:
  slskd:
    image: slskd/slskd
    container_name: slskd
    ports:
      - "5000:5000"
      - "5001:5001"
      - "50000:50000"
    volumes:
      - <path/to/application/data>:/app
    restart: always
```

This command or docker-compose file (depending on your choice) starts a container instance of slskd on ports 5000 (http) and 5001 (https), begins listening for incoming connections on port 50000, and maps the application directory to the provided path.

A more in-depth guide to running slskd in Docker can be found [here](https://github.com/slskd/slskd/blob/master/docs/docker.md).

### With Binaries

The latest stable binaries can be downloaded from the [releases](https://github.com/slskd/slskd/releases) page.  Platform specific binaries, along with the static content for the web UI, are produced as artifacts from every [build](https://github.com/slskd/slskd/actions?query=workflow%3ACI), if you'd prefer to use a canary release.

Binaries are shipped as zip files; extract the zip to a directory of your choosing and run.

An application directory will be created in either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows).

## Configuration

Once running, log in to the web UI using the default username `slskd` and password `slskd` to complete the configuration.

Detailed documentation for configuration options can be found [here](https://github.com/slskd/slskd/blob/master/docs/config.md), and an example of the yaml configuration file can be reviewed [here](https://github.com/slskd/slskd/blob/master/config/slskd.example.yml).
