# Running in Docker

You'll need to [install Docker](https://docs.docker.com/get-docker/) first.

Next, you'll need to make a few choices:

* The HTTP and/or HTTPS ports for the slskd web UI (defaults 5030 and 5031)
* The port for incoming connections from the Soulseek network (default 50300)
* The directory for the slskd application data

For most users, a quick start will be all that is needed:

```shell
docker run -d \
  -p <HTTP port>:5030 \
  -p <HTTPS port>:5031 \
  -p <listen port>:50300 \
  -v <path/to/application/data>:/app \
  --name slskd \
  slskd/slskd:latest
```

This configuration, however, doesn't include any shared directories.

First, you need to map each share to the container as a volume. Then each local directory within the container needs to be added to the configuration.

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
  slskd/slskd:latest
```

And the YAML configuration file would contain:

```yaml
directories:
  shared:
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
  slskd/slskd:latest
```