# slskd

[![Build](https://img.shields.io/github/actions/workflow/status/slskd/slskd/ci.yml?branch=master&logo=github)](https://github.com/sredevopsorg/slskd/actions/workflows/ci.yml)
[![Docker Pulls](https://img.shields.io/docker/pulls/slskd/slskd?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![GitHub all releases](https://img.shields.io/github/downloads/slskd/slskd/total?logo=github&color=brightgreen)](https://github.com/sredevopsorg/slskd/releases)
[![Contributors](https://img.shields.io/github/contributors/slskd/slskd?logo=github)](https://github.com/sredevopsorg/slskd/graphs/contributors)
[![Discord](https://img.shields.io/discord/971446666257391616?label=Discord&logo=discord)](https://slskd.org/discord)
[![Matrix](https://img.shields.io/badge/Matrix-%3F%20online-na?logo=matrix&color=brightgreen)](https://slskd.org/matrix)

A modern client-server application for the [Soulseek](https://www.slsknet.org/news/) file-sharing network.

## Features

### Secure access

slskd runs as a daemon or Docker container in your network (or in the cloud!) and is accessible from a web browser.  It's designed to be exposed to the internet, and everything is secured with a token that [you can control](https://github.com/sredevopsorg/slskd/blob/master/docs/config.md#authentication).  It also supports [reverse proxies](https://github.com/sredevopsorg/slskd/blob/master/docs/reverse_proxy.md), making it work well with other self-hosted tools.

![image](https://user-images.githubusercontent.com/17145758/193290217-0e6d87f5-a547-4451-8d90-d554a902716c.png)

### Search

Search for things just like you're used to with the official Soulseek client.  slskd makes it easy to enter multiple searches quickly.

![image](https://user-images.githubusercontent.com/17145758/193286989-30bd524d-81b6-4721-bd72-e4438c2b7b69.png)

### Results

Sort and filter search results using the same filters you use today.  Dismiss results you're not interested in, and download the ones you want in a couple of clicks.

![image](https://user-images.githubusercontent.com/17145758/193288396-dc3cc83d-6d93-414a-93f6-cea0696ac245.png)

### Downloads

Monitor the speed and status of downloads, grouped by user and folder.  Click the progress bar to fetch your place in queue, and use the selection tools to cancel, retry, or clear completed downloads.  Use the controls at the top to quickly manage downloads by status.

![image](https://user-images.githubusercontent.com/17145758/193289840-3aee153f-3656-4f15-b086-8b1ca25d38bb.png)

### Pretty much everything else

slskd can do almost everything the official Soulseek client can; browse user shares, join chat rooms, privately chat with other users.

New features are added all the time!

## Quick Start

### With Docker

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
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
      - "5030:5030"
      - "5031:5031"
      - "50300:50300"
    environment:
      - SLSKD_REMOTE_CONFIGURATION=true
    volumes:
      - <path/to/application/data>:/app
    restart: always
```

This command or docker-compose file (depending on your choice) starts a container instance of slskd on ports 5030 (HTTP) and 5031 (HTTPS using a self-signed certificate). slskd begins listening for incoming connections on port 50300 and maps the application directory to the provided path.

Once the container is running you can access the web UI over HTTP on port 5030, or HTTPS on port 5031.  The default username and password are `slskd` and `slskd`, respectively.  You'll want to change these if the application will be internet facing.

The `SLSKD_REMOTE_CONFIGURATION` environment variable allows you to modify application configuration settings from the web UI.  You might not want to enable this for an internet-facing installation.

You can find a more in-depth guide to running slskd in Docker [here](https://github.com/sredevopsorg/slskd/blob/master/docs/docker.md).

### With Binaries

The latest stable binaries can be downloaded from the [releases](https://github.com/sredevopsorg/slskd/releases) page. Platform-specific binaries and the static content for the Web UI are produced as artifacts from every [build](https://github.com/sredevopsorg/slskd/actions?query=workflow%3ACI) if you'd prefer to use a canary release.

Binaries are shipped as zip files; extract the zip to your chosen directory and run.

An application directory will be created in either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows).  In the root of this directory the file `slskd.yml` will be created the first time the application runs.  Edit this file to enter your credentials for the Soulseek network, and tweak any additional settings using the [configuration guide](https://github.com/sredevopsorg/slskd/blob/master/docs/config.md).

## Configuration

Once running, log in to the web UI using the default username `slskd` and password `slskd` to complete the configuration.

Detailed documentation for configuration options can be found [here](https://github.com/sredevopsorg/slskd/blob/master/docs/config.md), and an example of the YAML configuration file can be reviewed [here](https://github.com/sredevopsorg/slskd/blob/master/config/slskd.example.yml).
