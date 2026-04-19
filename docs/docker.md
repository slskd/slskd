# Running in Docker

You'll need to [install Docker](https://docs.docker.com/get-docker/) first.

Next, you'll need to make a few choices:

* The HTTP and/or HTTPS ports for the slskd web UI (defaults 5030 and 5031)
* The port for incoming connections from the Soulseek network (default 50300)
* The directory for the slskd application data
* How you'll specify the user the container will run as (important for downloaded file permissions!)

Two methods for specifying a user:
* Docker's built-in functionality; specify `--user` or `-u` in your `docker run` command, or add the `user:` key to your docker compose file
* The Linuxserver/*arr `PUID`/`PGID` method of passing user and group ID values in environment variables

Docker's built-in functionality is objectively superior; the container starts and runs as the user you specify, it never has root privileges.  The `PUID`/`PGID` method starts the container as root, updates the built-in `slskd` user's user and group IDs to match the values you provided, and changes the owner of your mounted app directory to that user.

For most users, a quick start will be all that is needed:

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

This configuration, however, doesn't include any shared directories, and any files that are downloaded will be owned by root.

First, you need to map each share to the container as a volume. Then each local directory within the container needs to be added to the configuration. You may also need to specify the user and group ID that should run the container and own files created by slskd. The default is `root:root`, but Docker will accept any numeric values in the `UID:GID` format, such as `1000:1000` in this example.

In the following example, assume that the slskd application directory will be `/var/slskd` on the docker host. Assume that the directories `/home/JohnDoe/Music` and `/home/JohnDoe/eBooks` will be shared. 


For this scenario, the `docker run` command would be:

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
  -v /var/slskd:/app \
  -v /home/JohnDoe/Music:/music \
  -v /home/JohnDoe/eBooks:/ebooks \
  --name slskd \
  --user 1000:1000 \
  slskd/slskd:latest
```

Or, for `docker-compose`:

```yaml
version: "3"
services:
  slskd:
    environment:
      - SLSKD_REMOTE_CONFIGURATION=true
    ports:
      - 5030:5030/tcp
      - 5031:5031/tcp
      - 50300:50300/tcp
    volumes:
      - /var/slskd:/app:rw
      - /home/JohnDoe/Music:/music:rw
      - /home/JohnDoe/eBooks:/ebooks:rw
    user: 1000:1000
    image: slskd/slskd:latest
```
The YAML configuration file would contain:

```yaml
shares:
  directories:
    - /music
    - /ebooks
```

You can achieve the same configuration by setting the `SLSKD_SHARED_DIR` environment variable in the `docker run` command:

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
  -v /var/slskd:/app \
  -v /home/JohnDoe/Music:/music \
  -v /home/JohnDoe/eBooks:/ebooks \
  -e "SLSKD_SHARED_DIR=/music;/ebooks" \
  --name slskd \
  --user 1000:1000 \
  slskd/slskd:latest
```

Or, for `docker-compose`:

```yaml
version: "3"
services:
  slskd:
    environment:
      - SLSKD_REMOTE_CONFIGURATION=true
      - "SLSKD_SHARED_DIR=/music;/ebooks"
    ports:
      - 5030:5030/tcp
      - 5031:5031/tcp
      - 50300:50300/tcp
    volumes:
      - /var/slskd:/app:rw
      - /home/JohnDoe/Music:/music:rw
      - /home/JohnDoe/eBooks:/ebooks:rw
    user: 1000:1000
    image: slskd/slskd:latest
```