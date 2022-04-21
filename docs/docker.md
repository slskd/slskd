# Running slskd in Docker

For most users, a quick start will be all that is needed:

```shell
docker run -d \
  -p 5000:5000 \
  -p 5001:5001 \
  -p 50000:50000 \
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
  -p 5000:5000 \
  -p 5001:5001 \
  -p 50000:50000 \
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

You can achieve the same configuration by setting the `SHARED_DIR` environment variable in the `docker run` command:

```shell
docker run -d \
  -p 5000:5000 \
  -p 5001:5001 \
  -p 50000:50000 \
  -v /var/slskd:/app \
  -v /home/JohnDoe/Music:/music \
  -v /home/JohnDoe/eBooks:/ebooks \
  -e "SLSKD_SHARED_DIR=/music;/ebooks" \
  --name slskd \
  slskd/slskd:latest
```