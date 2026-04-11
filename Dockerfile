# syntax=docker/dockerfile:1
# ^ enable heredoc for BuildKit

# build static web content
# note: pin this to amd64 to speed it up, it is prohibitively slow under QEMU
FROM --platform=$BUILDPLATFORM node:22-alpine AS web
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY bin bin/.
COPY src/web src/web/.

RUN sh ./bin/build --web-only --version $VERSION

# build, test, and publish application binaries
# note: this needs to be pinned to an amd64 image in order to publish armv7 binaries
# https://github.com/dotnet/dotnet-docker/issues/1537#issuecomment-615269150
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS publish
ARG TARGETPLATFORM
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY config config/.
COPY src/slskd src/slskd/.
COPY tests tests/.

COPY --from=web /slskd/src/web/build /slskd/src/slskd/wwwroot/.

RUN bash ./bin/build --dotnet-only --version $VERSION

RUN bash ./bin/publish --no-prebuild --platform $TARGETPLATFORM --version $VERSION --output ../../dist/${TARGETPLATFORM}

# application
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS slskd
ARG TARGETPLATFORM
ARG TAG=0.0.1
ARG VERSION=0.0.1.65534-local
ARG REVISION=0
ARG BUILD_DATE

RUN apt-get update && apt-get install --no-install-recommends -y \
  jq \
  wget \
  tini \
  gosu \
  && \
  rm -rf \
  /tmp/* \
  /var/lib/apt/lists/* \
  /var/cache/apt/* \
  /var/tmp/*

# remove the default 'ubuntu' user that occupies 1000:1000
# and replace it with our own slskd user/group
RUN userdel -r ubuntu && \
  groupadd -g 1000 slskd && \
  useradd -u 1000 -g slskd -d /app -s /sbin/nologin slskd

RUN bash -c 'mkdir -p /app/{incomplete,downloads} \
  && chown -R slskd:slskd /app \
  && mkdir -p /.net \
  && chown slskd:slskd /.net'

VOLUME /app

HEALTHCHECK --interval=60s --timeout=3s --start-period=60m --retries=3 CMD wget -q -O - http://localhost:${SLSKD_HTTP_PORT}/health

ENV SHELL=/usr/bin/bash \
  DOTNET_EnableDiagnostics=0 \
  DOTNET_BUNDLE_EXTRACT_BASE_DIR=/.net \
  DOTNET_gcServer=0 \
  DOTNET_gcConcurrent=1 \
  DOTNET_GCHeapHardLimit=0x80000000 \
  DOTNET_GCConserveMemory=9 \
  SLSKD_UMASK=0022 \
  SLSKD_HTTP_PORT=5030 \
  SLSKD_HTTPS_PORT=5031 \
  SLSKD_SLSK_LISTEN_PORT=50300 \
  SLSKD_APP_DIR=/app \
  SLSKD_DOCKER_TAG=$TAG \
  SLSKD_DOCKER_VERSION=$VERSION \
  SLSKD_DOCKER_REVISON=$REVISION \
  SLSKD_DOCKER_BUILD_DATE=$BUILD_DATE

LABEL org.opencontainers.image.title=slskd \
  org.opencontainers.image.description="A modern client-server application for the Soulseek file sharing network" \
  org.opencontainers.image.authors="slskd Team" \
  org.opencontainers.image.vendor="slskd Team" \
  org.opencontainers.image.licenses=AGPL-3.0 \
  org.opencontainers.image.url=https://slskd.org \
  org.opencontainers.image.source=https://github.com/slskd/slskd \
  org.opencontainers.image.documentation=https://github.com/slskd/slskd \
  org.opencontainers.image.version=$VERSION \
  org.opencontainers.image.revision=$REVISION \
  org.opencontainers.image.created=$BUILD_DATE

WORKDIR /slskd
COPY --from=publish /slskd/dist/${TARGETPLATFORM} .

# supports two modes:
#   1. PUID/PGID (legacy linuxserver style) — container starts as root,
#      mutates user, chowns /app if needed, drops privileges via gosu.
#   2. --user / user: (modern Docker) — container starts as non-root,
#      skips all usermod/chown, execs the app directly.
COPY <<'SCRIPT' /entrypoint.sh
#!/bin/bash
set -e

# --user mode; user has provided a user via the command line or docker compose file
#---------------------------------------------------------------------------------------
if [ "$(id -u)" -ne 0 ]; then
    # if the user has also supplied PUID or PGID, let them know they need to use one or the other
    if [ -n "${PUID}" ] || [ -n "${PGID}" ]; then
        echo "ERROR: PUID/PGID are set but the container is running as a non-root user (using --user or user:)."
        echo "Use one or the other, not both. Remove --user and set PUID/PGID, or remove PUID/PGID and use --user."
        exit 1
    fi

    # exit if /app is not writable
    if [ ! -r /app ] || [ ! -w /app ]; then
        echo "ERROR: /app is not readable and/or writable by the current user ($(id -u):$(id -g))."
        echo "When using --user, ensure the mounted directory is readable and writable by UID $(id -u)."
        exit 1
    fi

    # set umask and launch
    umask "$SLSKD_UMASK"
    exec "$@"
fi

# PUID/PGID mode; user has provided explicit user and group IDs to use
#---------------------------------------------------------------------------------------
PUID="${PUID:-1000}"
PGID="${PGID:-1000}"

if ! [[ "${PUID}" =~ ^[0-9]+$ ]]; then
    echo "ERROR: PUID must be a non-negative integer, got '${PUID}'."
    exit 1
fi

if ! [[ "${PGID}" =~ ^[0-9]+$ ]]; then
    echo "ERROR: PGID must be a non-negative integer, got '${PGID}'."
    exit 1
fi

# update group
current_gid=$(getent group slskd | cut -d: -f3)
echo "[entrypoint] current GID: ${current_gid}"
if [ "${current_gid}" != "${PGID}" ]; then
    groupmod -o -g "${PGID}" slskd
    usermod -g "${PGID}" slskd
    echo "[entrypoint] new GID: ${PGID}"
fi

# update user
current_uid=$(id -u slskd 2>/dev/null || echo "")
echo "[entrypoint] current UID: ${current_uid}"
if [ "${current_uid}" != "${PUID}" ]; then
    usermod -o -u "${PUID}" slskd
    echo "[entrypoint] new UID: ${PUID}"
fi

# change owner of the directories we need to write
chown -R "${PUID}:${PGID}" /app
chown "${PUID}:${PGID}" /.net

# set umask and launch
umask "$SLSKD_UMASK"
exec gosu "${PUID}:${PGID}" "$@"

SCRIPT
# EOF

RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/usr/bin/tini", "--", "/entrypoint.sh"]
CMD ["./slskd"]