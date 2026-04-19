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

RUN groupadd -o -g 1000 slskd && \
  useradd -o  -u 1000 -g slskd slskd

RUN bash -c 'mkdir -p /.net \
  && chmod 777 /.net'

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
  SLSKD_DOCKER_REVISION=$REVISION \
  SLSKD_DOCKER_BUILD_DATE=$BUILD_DATE

LABEL org.opencontainers.image.title=slskd \
  org.opencontainers.image.description="A modern client-server application for the Soulseek file sharing network" \
  org.opencontainers.image.authors="JP Dillingham, slskd Contributors" \
  org.opencontainers.image.vendor="slskd Project" \
  org.opencontainers.image.licenses=AGPL-3.0 \
  org.opencontainers.image.url=https://slskd.org \
  org.opencontainers.image.source=https://github.com/slskd/slskd \
  org.opencontainers.image.documentation=https://github.com/slskd/slskd \
  org.opencontainers.image.version=$VERSION \
  org.opencontainers.image.revision=$REVISION \
  org.opencontainers.image.created=$BUILD_DATE

WORKDIR /slskd
COPY --from=publish /slskd/dist/${TARGETPLATFORM} .

# supports three modes:
#   1. --user / user: (modern Docker) — container starts as non-root, skips all usermod/chown, execs the app directly.
#   2. PUID/PGID (legacy linuxserver style) — container starts as root, mutates user, chowns /app if needed, drops privileges via gosu.
#   3. neither --user nor at least one of PUID/PGID supplied - container and app run as root
COPY <<'SCRIPT' /entrypoint.sh
#!/bin/bash
set -e

# --user mode; user has provided a user via the command line or docker compose file
#---------------------------------------------------------------------------------------
if [ "$(id -u)" -ne 0 ]; then
    # if the user has also supplied PUID or PGID, let them know they need to use one or the other
    if [ -n "${PUID}" ] || [ -n "${PGID}" ]; then
        echo "ERROR: PUID/PGID are set but the container is running as a non-root user (using --user or user:)."
        echo "Use one or the other, not both. Either remove --user and set PUID/PGID, or remove PUID/PGID and use --user."
        exit 1
    fi

    # attempt to create the app dir; swallow the error if it fails, we'll give the user an informative message below
    if [ ! -d "${SLSKD_APP_DIR}" ]; then
        mkdir -p "${SLSKD_APP_DIR}" 2>/dev/null || true
    fi

    # exit if app dir is not writable; we can't fix this because we lack permissions
    if [ ! -r "${SLSKD_APP_DIR}" ] || [ ! -w "${SLSKD_APP_DIR}" ]; then
        echo "ERROR: ${SLSKD_APP_DIR} is not readable and/or writable by the current user ($(id -u):$(id -g)); currently owned by $(stat -c '%U:%G (%u:%g)' ${SLSKD_APP_DIR})."
        echo "To fix: chown -R $(id -u):$(id -g) /path/to/your/mounted/app/directory"
        exit 1
    fi

    if ! mountpoint -q "${SLSKD_APP_DIR}"; then
        echo "WARNING: ${SLSKD_APP_DIR} is not a bind mount or volume. Configuration and data will be lost if the container is removed."
    fi

    umask "$SLSKD_UMASK"
    exec "$@"
fi

# no --user supplied, neither PUID nor PGID supplied, run as root (same behavior as < 0.25.0)
#---------------------------------------------------------------------------------------
if [ -z "${PUID}" ] && [ -z "${PGID}" ]; then
    # create app dir as root, if it doesn't exist
    # no need to change the owner of anything; we can write
    mkdir -p "${SLSKD_APP_DIR}"

    if ! mountpoint -q "${SLSKD_APP_DIR}"; then
        echo "WARNING: ${SLSKD_APP_DIR} is not a bind mount or volume. Configuration and data will be lost if the container is removed."
    fi

    umask "$SLSKD_UMASK"
    exec "$@"
fi

# PUID/PGID mode; user has provided explicit user and/or group IDs to use
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

# update group if needed
if [ "$(getent group slskd | cut -d: -f3)" != "$PGID" ]; then
    groupmod -o -g "$PGID" slskd
fi

# update user if needed
if [ "$(id -u slskd)" != "$PUID" ] || [ "$(id -g slskd)" != "$PGID" ]; then
    usermod -o -u "$PUID" -g "$PGID" slskd
fi

# create app dir as root, if it doesn't exist
mkdir -p "${SLSKD_APP_DIR}"

if ! mountpoint -q "${SLSKD_APP_DIR}"; then
    echo "WARNING: ${SLSKD_APP_DIR} is not a bind mount or volume. Configuration and data will be lost if the container is removed."
fi

# change owner of the the app directory if it isn't correct
# we're not checking anything other than the root, and we're not recursively
# applying the changes, which might be dangerous/unwanted and/or take forever
if [ "$(stat -c '%u:%g' "${SLSKD_APP_DIR}")" != "${PUID}:${PGID}" ]; then
    chown "${PUID}:${PGID}" "${SLSKD_APP_DIR}"
fi

umask "$SLSKD_UMASK"
exec gosu "${PUID}:${PGID}" "$@"

SCRIPT
# EOF

RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/usr/bin/tini", "--", "/entrypoint.sh"]
CMD ["./slskd"]
