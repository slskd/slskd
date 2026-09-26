# slskd (qol fork)

[![Fork build](https://img.shields.io/github/actions/workflow/status/xctwt/slskd/fork-build.yml?branch=nicotine-qol&logo=github&label=fork%20build)](https://github.com/xctwt/slskd/actions/workflows/fork-build.yml)
[![Build](https://img.shields.io/github/actions/workflow/status/slskd/slskd/ci.yml?branch=master&logo=github)](https://github.com/slskd/slskd/actions/workflows/ci.yml)
[![Docker Pulls](https://img.shields.io/docker/pulls/slskd/slskd?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![GitHub all releases](https://img.shields.io/github/downloads/slskd/slskd/total?logo=github&color=brightgreen)](https://github.com/slskd/slskd/releases)
[![Contributors](https://img.shields.io/github/contributors/slskd/slskd?logo=github)](https://github.com/slskd/slskd/graphs/contributors)
[![Discord](https://img.shields.io/discord/971446666257391616?label=Discord&logo=discord)](https://slskd.org/discord)
[![Matrix](https://img.shields.io/badge/Matrix-%3F%20online-na?logo=matrix&color=brightgreen)](https://slskd.org/matrix)

A modern client-server application for the [Soulseek](https://www.slsknet.org/news/) file-sharing network.

> [!NOTE]
> This is a fork of [slskd/slskd](https://github.com/slskd/slskd) that adds release lookups, a settings editor and other quality-of-life features inspired by [Nicotine+](https://nicotine-plus.org/). It works with an existing slskd configuration and data. The badges above other than **fork build** are for upstream slskd. Please report problems with this fork's features here, not upstream.

## Differences from upstream slskd

### Releases (MusicBrainz)

A new **Releases** page finds an album on MusicBrainz first, then looks for it on Soulseek.

- Search by `Artist - Album`, plain text, a MusicBrainz release, release-group or artist link, or a bare release ID.
- See each release's track list, format, label and catalog number, with cover art from the Cover Art Archive. Covers the archive doesn't have are looked up on Deezer and iTunes.
- Search Soulseek for a release in one click. Matching folders are ranked by how many of the release's tracks they hold (by track number, title and length) and by file quality. Download the matched tracks, with or without the folder's other files (cover art, logs), into an `Artist - Album (Year)` folder.
- Releases you search for are saved and linked to their Soulseek searches, so you can come back to them later.

The new `integrations.musicbrainz` options are all optional:

```yaml
integrations:
  musicbrainz:
    disabled: false
    url: https://musicbrainz.org   # point at a mirror to avoid the public rate limit
    request_interval: 1000         # milliseconds between requests; musicbrainz.org allows one per second
    cover_fallback: true           # look up missing covers on Deezer and iTunes
```

### Settings page

A **Settings** page edits `slskd.yml` through forms, organized like Nicotine+'s preferences (Profile, Network, Shares, Downloads, Uploads, Users & Bans, Searches & Rooms, Web & Security, Cleanup, Integrations). It includes:

- An editor for user groups and ban lists.
- A profile editor for the description and picture other users see, with a preview. Uploaded pictures are stored in `<app dir>/profile/`.

Changes are validated by the server and saved together. Only the keys you changed are written; comments, ordering and keys the page doesn't know about are left as they were.

The page needs `remote_configuration: true` in `slskd.yml`, the same as upstream's built-in YAML editor.

### Users

- Every user has a profile page at `/users/<username>`, showing their status, statistics, group, description, picture and shared files. You can ban or unban them there, or gift them days of privileges.
- Clicking a username anywhere (search results, transfers, chat, rooms) opens that user in a side panel without leaving the page.
- Right-clicking a username opens a menu: **View Profile**, **Browse Files**, **Send Message…**, **Open in Users Tab** and **Copy Username**.

### Searches

- A filter form next to the filter text box, with minimum and maximum size and length, minimum upload speed, maximum queue length, minimum files per folder, required and excluded words, and formats. Filters can be saved and reused.
- A **Clear all** button that deletes every finished search.

### Fixes

- On Windows, paths derived from `--app-dir` are normalized, so values with forward slashes (e.g. `C:/slskd`) no longer break downloads.
- Validating options no longer fails when two requests read the config file at the same time.
- Error messages survive reverse proxies. Cloudflare replaces a 502 from the server with its own error page, so MusicBrainz failures are now reported as 503, with the reason shown in the UI and logged.

### Builds

- There is no Docker image for this fork. Every push to `nicotine-qol` publishes Linux builds (`linux-x64` and `linux-arm64`) to the rolling [`qol-latest`](https://github.com/xctwt/slskd/releases/tag/qol-latest) release.
- [`bin/update-vps`](bin/update-vps) installs the latest build over an existing systemd install and rolls back if it doesn't start.
- Fork builds are versioned after the upstream release they're based on, e.g. `0.26.0.65534+abc1234`.

## Installing this fork

### Linux server (systemd)

1. Create a user and download the latest build. Use `slskd-linux-arm64.tar.gz` on ARM machines:

   ```sh
   sudo useradd --system --create-home --home-dir /var/lib/slskd slskd
   sudo mkdir -p /opt/slskd
   curl -fL https://github.com/xctwt/slskd/releases/download/qol-latest/slskd-linux-x64.tar.gz \
     | sudo tar -xz -C /opt/slskd
   sudo chown -R slskd:slskd /opt/slskd
   ```

2. Create `/etc/systemd/system/slskd.service`:

   ```ini
   [Unit]
   Description=slskd
   After=network-online.target
   Wants=network-online.target

   [Service]
   Type=simple
   User=slskd
   Group=slskd
   ExecStart=/opt/slskd/slskd --app-dir /var/lib/slskd
   Restart=on-failure

   [Install]
   WantedBy=multi-user.target
   ```

3. Start it:

   ```sh
   sudo systemctl daemon-reload
   sudo systemctl enable --now slskd
   ```

   On the first run slskd creates `/var/lib/slskd/slskd.yml`. Edit it to add your Soulseek username and password, change the web UI login (the default is `slskd` / `slskd`), and set `remote_configuration: true` if you want to use the Settings page. Then run `sudo systemctl restart slskd`. [`config/slskd.example.yml`](config/slskd.example.yml) lists every option.

4. Open `http://<server>:5030`. If you put it behind a reverse proxy or Cloudflare, see the [reverse proxy guide](docs/reverse_proxy.md), and make sure WebSockets are allowed. Search results and transfers update over a WebSocket connection.

### Docker

Build the image from this repository, then run it exactly as you would upstream's image (see [Quick Start](#quick-start)), using `slskd-qol` in place of `slskd/slskd`:

```sh
git clone -b nicotine-qol https://github.com/xctwt/slskd.git
cd slskd
docker build -t slskd-qol .
```

### From source

Building needs the .NET 10 SDK, Node.js 22 and bash:

```sh
git clone -b nicotine-qol https://github.com/xctwt/slskd.git
cd slskd
./bin/build                              # builds and tests the web UI and the server
./bin/publish --runtime linux-x64        # self-contained build in dist/linux-x64
```

## Updating

- **systemd:** run the update script on the server:

  ```sh
  curl -fsSL https://raw.githubusercontent.com/xctwt/slskd/nicotine-qol/bin/update-vps | sudo bash
  ```

  The script finds the install folder from the `slskd` service. Set `SERVICE=<name>` or `INSTALL_DIR=<folder>` if yours is different. It stops the service, keeps the current version in `<install folder>.previous`, installs the new build, and puts the previous version back if the new one doesn't stay up. Your config and data are not touched.

- **Docker:** run `git pull`, rebuild the image, and recreate the container.

## Migrating from upstream slskd

This fork uses the same application directory as upstream: the same `slskd.yml`, the same database in `data/`, and the same logs. Nothing needs converting.

1. **Back up your application directory.** This is the folder with `slskd.yml` in it: the path given to `--app-dir`, `~/.local/share/slskd` by default, or the `/app` volume in Docker. For example, `sudo tar -czf ~/slskd-backup.tar.gz -C /var/lib slskd`.
2. **Install the fork over upstream:**
   - **Upstream binaries under systemd:** run the update script from [Updating](#updating). It detects the install folder from your service and replaces only the binaries and web UI.
   - **Upstream Docker image:** build `slskd-qol` as described under [Docker](#docker). Stop the old container, then start a new one with the same volumes, ports and environment variables, but with `image: slskd-qol`.
   - **Upstream binaries started by hand:** stop slskd, extract the fork's build over the old folder (delete the old `wwwroot` folder first), and start it with the same `--app-dir`.
3. **Optional:** set `remote_configuration: true` to use the Settings page, and add an `integrations.musicbrainz` section if the defaults don't suit you.
4. Open the web UI and hard-refresh (Ctrl+F5). Your browser may still have the old UI cached.

### Going back to upstream

1. Remove the `integrations.musicbrainz` section from `slskd.yml`, if you added one. Upstream slskd ignores it at startup, but its built-in config editor refuses to save a file containing keys it doesn't recognize.
2. Reinstall upstream: extract an upstream [release](https://github.com/slskd/slskd/releases) over the install folder (delete `wwwroot` first), or switch the container back to `slskd/slskd`.
3. `data/releases.json` (saved releases) is only used by this fork, so you can delete it. Uploaded profile pictures in `profile/` keep working if `soulseek.picture` points at one.

## Features

### Secure access

slskd runs as a daemon or Docker container in your network (or in the cloud!) and is accessible from a web browser.  It's designed to be exposed to the internet, and everything is secured with a token that [you can control](https://github.com/slskd/slskd/blob/master/docs/config.md#authentication).  It also supports [reverse proxies](https://github.com/slskd/slskd/blob/master/docs/reverse_proxy.md), making it work well with other self-hosted tools.

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

Choose Docker's built-in method of specifying a user for the container:

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
  -v <path/to/application/data>:/app \
  --name slskd \
  --user 1000:1000 \
  slskd/slskd:latest
```

Or use the Linuxserver/*arr `PUID`/`PGID` method:

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e PUID=1000 \
  -e PGID=1000 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
  -v <path/to/application/data>:/app \
  --name slskd \
  slskd/slskd:latest
```

### With Docker-Compose

Choose Docker's built-in method of specifying a user for the container:

```yaml
services:
  slskd:
    image: slskd/slskd
    container_name: slskd
    user: "1000:1000"
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

Or use the Linuxserver/*arr `PUID`/`PGID` method:

```yaml
services:
  slskd:
    image: slskd/slskd
    container_name: slskd
    ports:
      - "5030:5030"
      - "5031:5031"
      - "50300:50300"
    environment:
      - PUID=1000
      - PGID=1000
      - SLSKD_REMOTE_CONFIGURATION=true
    volumes:
      - <path/to/application/data>:/app
    restart: always
```

This command or docker-compose file (depending on your choice) starts a container instance of slskd on ports 5030 (HTTP) and 5031 (HTTPS using a self-signed certificate). slskd begins listening for incoming connections on port 50300 and maps the application directory to the provided path.

Once the container is running you can access the web UI over HTTP on port 5030, or HTTPS on port 5031.  The default username and password are `slskd` and `slskd`, respectively.  You'll want to change these if the application will be internet facing.

The `SLSKD_REMOTE_CONFIGURATION` environment variable allows you to modify application configuration settings from the web UI.  You might not want to enable this for an internet-facing installation.

You can find a more in-depth guide to running slskd in Docker [here](https://github.com/slskd/slskd/blob/master/docs/docker.md).

### With Binaries

The latest stable binaries can be downloaded from the [releases](https://github.com/slskd/slskd/releases) page. Platform-specific binaries and the static content for the Web UI are produced as artifacts from every [build](https://github.com/slskd/slskd/actions?query=workflow%3ACI) if you'd prefer to use a canary release.

Binaries are shipped as zip files; extract the zip to your chosen directory and run.

An application directory will be created in either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows).  In the root of this directory the file `slskd.yml` will be created the first time the application runs.  Edit this file to enter your credentials for the Soulseek network, and tweak any additional settings using the [configuration guide](https://github.com/slskd/slskd/blob/master/docs/config.md).

## Configuration

Once running, log in to the web UI using the default username `slskd` and password `slskd` to complete the configuration.

Detailed documentation for configuration options can be found [here](https://github.com/slskd/slskd/blob/master/docs/config.md), and an example of the YAML configuration file can be reviewed [here](https://github.com/slskd/slskd/blob/master/config/slskd.example.yml).

## Reverse Proxy
SLSKD may require extra configuration when running it behind a reverse proxy. Refer [here](https://github.com/slskd/slskd/blob/master/docs/reverse_proxy.md) for a short guide.
