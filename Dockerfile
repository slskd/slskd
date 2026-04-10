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

RUN groupadd -g 1000 slskd && \
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
COPY <<'ENTRYPOINT' /usr/local/bin/docker-entrypoint.sh
#!/bin/bash
set -e

SLSKD_USER="slskd"
SLSKD_GROUP="slskd"
DEFAULT_PUID=1000
DEFAULT_PGID=1000

# --user mode
if [ "$(id -u)" -ne 0 ]; then
    umask "$SLSKD_UMASK"
    exec "$@"
fi

# PUID/PGID mode

PUID="${PUID:-$DEFAULT_PUID}"
PGID="${PGID:-$DEFAULT_PGID}"

echo "
───────────────────────────────────────
  slskd
───────────────────────────────────────
  PUID:  ${PUID}
  PGID:  ${PGID}
  UMASK: ${SLSKD_UMASK}
───────────────────────────────────────"

if [ "${PUID}" = "0" ] || [ "${PGID}" = "0" ]; then
    echo "[warn] Running as root (PUID=0 or PGID=0) is not recommended."
fi

# Mutate group
current_gid=$(id -g "${SLSKD_USER}" 2>/dev/null || echo "")
if [ "${current_gid}" != "${PGID}" ]; then
    groupmod -o -g "${PGID}" "${SLSKD_GROUP}"
fi

# Mutate user — park homedir at /tmp first to prevent usermod from doing an
# implicit recursive chown of the home directory (linuxserver technique)
current_uid=$(id -u "${SLSKD_USER}" 2>/dev/null || echo "")
if [ "${current_uid}" != "${PUID}" ]; then
    orig_home=$(getent passwd "${SLSKD_USER}" | cut -d: -f6)
    usermod -d /tmp "${SLSKD_USER}"
    usermod -o -u "${PUID}" "${SLSKD_USER}"
    usermod -d "${orig_home}" "${SLSKD_USER}"
fi

# Fix /app ownership, but only when PUID/PGID changed (binhex marker pattern).
# /app has only config, logs, and databases — always a small number of files.
# Shared volumes (/music, /downloads elsewhere, etc.) are never touched; the
# process runs as PUID:PGID so new files get correct ownership automatically.
prev_puid=$(cat /app/.slskd_puid 2>/dev/null || true)
prev_pgid=$(cat /app/.slskd_pgid 2>/dev/null || true)

if [ "${prev_puid}" != "${PUID}" ] || [ "${prev_pgid}" != "${PGID}" ]; then
    echo "[entrypoint] Applying ownership to /app (PUID:PGID = ${PUID}:${PGID})..."
    chown -R "${PUID}:${PGID}" /app
    echo "${PUID}" > /app/.slskd_puid
    echo "${PGID}" > /app/.slskd_pgid
    chown "${PUID}:${PGID}" /app/.slskd_puid /app/.slskd_pgid
fi

# Ensure /.net is accessible (dotnet runtime extraction cache)
chown "${PUID}:${PGID}" /.net 2>/dev/null || true

# Drop privileges and exec
umask "$SLSKD_UMASK"
exec gosu "${PUID}:${PGID}" "$@"
ENTRYPOINT
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/docker-entrypoint.sh"]
CMD ["./slskd"]