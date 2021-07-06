FROM node:lts-alpine3.13 AS web
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY bin bin/.
COPY src/web src/web/.

RUN sh ./bin/build --web-only --version $VERSION

# note: this needs to be pinned to an amd64 image in order to publish armv7 binaries
# https://github.com/dotnet/dotnet-docker/issues/1537#issuecomment-615269150
FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim-amd64 AS publish
ARG TARGETPLATFORM
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY src/slskd src/slskd/.
COPY tests tests/.

COPY --from=web /slskd/src/web/build /slskd/src/slskd/wwwroot/.

RUN bash ./bin/build --dotnet-only --version $VERSION

RUN bash ./bin/publish --no-prebuild --platform $TARGETPLATFORM --version $VERSION --output ../../dist/${TARGETPLATFORM}

#

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-buster-slim AS slskd
ARG TARGETPLATFORM
ARG TAG=0.0.1
ARG VERSION=0.0.1.65534-local
ARG REVISION=0
ARG BUILD_DATE

LABEL org.opencontainers.image.title=slskd \
  org.opencontainers.image.description="a modern client-server application for the Soulseek file sharing network" \
  org.opencontainers.image.authors="slskd Team" \
  org.opencontainers.image.vendor="slskd Team" \
  org.opencontainers.image.licenses=AGPL-3.0 \
  org.opencontainers.image.url=https://slskd.org \
  org.opencontainers.image.source=https://github.com/slskd/slskd \
  org.opencontainers.image.documentation=https://github.com/slskd/slskd \
  org.opencontainers.image.version=$VERSION \
  org.opencontainers.image.revision=$REVISION \
  org.opencontainers.image.created=$BUILD_DATE

RUN apt-get update && apt-get install -y \
  wget \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /slskd
COPY --from=publish /slskd/dist/${TARGETPLATFORM} .

RUN bash -c 'mkdir -p /var/slskd/{incomplete,downloads,shared} \ 
  && chmod 777 /var/slskd \
  && chmod 777 /var/slskd/{incomplete,downloads,shared}'

ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/var/tmp/.net \
    SLSKD_HTTP_PORT=5000 \
    SLSKD_APP_DIR=/var/slskd \
    SLSKD_INCOMPLETE_DIR=/var/slskd/incomplete \
    SLSKD_DOWNLOADS_DIR=/var/slskd/downloads \
    SLSKD_SHARED_DIR=/var/slskd/shared \
    SLSKD_DOCKER_TAG=$TAG \
    SLSKD_DOCKER_VERSION=$VERSION \
    SLSKD_DOCKER_REVISON=$REVISION \
    SLSKD_DOCKER_BUILD_DATE=$BUILD_DATE

HEALTHCHECK --interval=60s --timeout=3s --start-period=5s --retries=3 CMD wget -q -O - http://localhost:5000/health

ENTRYPOINT ["./slskd"]