# slskd

slskd is a modern client-server application for the Soulseek file sharing network.

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
    environment:
      - PUID=1000
      - PGID=1000
    volumes:
      - <path/to/application/data>:/app
    restart: always
```

This command or docker-compose file (depending on your choice) starts a container instance of slskd on ports 5000 (http) and 5001 (https), begins listening for incoming connections on port 50000, and maps the application directory to the provided path.

### With Binaries

The latest stable binaries can be downloaded from the [releases](https://github.com/slskd/slskd/releases) page.  Platform specific binaries, along with the static content for the web UI, are produced as artifacts from every [build](https://github.com/slskd/slskd/actions?query=workflow%3ACI), if you'd prefer to use a canary release.

Binaries are shipped as zip files; extract the zip to a directory of your choosing and run.

An application directory will be created in either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows).

## Configuration

Once running, log in to the web UI using the default username `slskd` and password `slskd` to complete the configuration.

Detailed documentation for configuration options can be found [here](https://github.com/slskd/slskd/blob/master/docs/config.md).
