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
ARG VERSION=0.0.1.65534-local

LABEL org.opencontainers.image.title=slskd
LABEL org.opencontainers.image.description="a modern client-server application for the Soulseek file sharing network"
LABEL org.opencontainers.image.url=https://slskd.org
LABEL org.opencontainers.image.source=https://github.com/slskd/slskd
LABEL org.opencontainers.image.licenses=AGPL-3.0
LABEL org.opencontainers.image.version=${VERSION}
LABEL org.opencontainers.image.created=$(date --iso-8601=s)

WORKDIR /slskd
COPY --from=publish /slskd/dist/${TARGETPLATFORM} .

RUN mkdir /var/slskd
RUN mkdir /var/slskd/incomplete
RUN mkdir /var/slskd/downloads
RUN mkdir /var/slskd/shared

ENV SLSKD_HTTP_PORT=5000
ENV SLSKD_APP_DIR=/var/slskd
ENV SLSKD_INCOMPLETE_DIR=/var/slskd/incomplete
ENV SLSKD_DOWNLOADS_DIR=/var/slskd/downloads
ENV SLSKD_SHARED_DIR=/var/slskd/shared

ENV SLSKD_DOCKER_VERSION=${VERSION}

HEALTHCHECK --interval=60s --timeout=3s --start-period=5s --retries=3 CMD wget -q -O - http://localhost:5000/health

ENTRYPOINT ["./slskd"]