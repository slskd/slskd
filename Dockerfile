FROM node:lts-alpine3.12 AS web
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY bin bin/.
COPY src/web src/web/.

RUN sh ./bin/build --web-only --version $VERSION

#

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY src/slskd src/slskd/.
COPY tests tests/.

RUN bash ./bin/build --dotnet-only --version $VERSION

#

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS publish
ARG TARGETPLATFORM
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY src/slskd src/slskd/.
COPY --from=web /slskd/src/web/build /slskd/src/slskd/wwwroot/.

RUN bash ./bin/publish --no-prebuild --platform $TARGETPLATFORM --version $VERSION --output ../../dist/${TARGETPLATFORM}

#

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine AS slskd
ARG TARGETPLATFORM
ARG VERSION=0.0.1.65534-local

LABEL org.opencontainers.image.source=https://github.com/slskd/slskd
LABEL org.opencontainers.image.licsense=AGPL-3.0
LABEL org.opencontainers.image.version=${VERSION}

WORKDIR /slskd
COPY --from=publish /slskd/dist/${TARGETPLATFORM} .

RUN mkdir /var/slskd
RUN mkdir /var/slskd/shared
RUN mkdir /var/slskd/downloads

ENV SLSKD_HTTP_PORT=5000
ENV SLSKD_SHARED_DIR=/var/slskd/shared
ENV SLSKD_DOWNLOADS_DIR=/var/slskd/downloads

ENV SLSKD_DOCKER_VERSION=${VERSION}

HEALTHCHECK --interval=60s --timeout=3s --start-period=5s --retries=3 CMD wget -q -O - http://localhost:5000/health

ENTRYPOINT ["./slskd"]