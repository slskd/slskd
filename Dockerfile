FROM node:lts-alpine3.12 AS web
ARG VERSION=0.0.1.65535-local

WORKDIR /slskd

COPY bin bin/.
COPY src/web src/web/.

RUN sh ./bin/build --web-only --version $VERSION

#

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
ARG VERSION=0.0.1.65535-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY src/slskd src/slskd/.
COPY tests tests/.

RUN bash ./bin/build --dotnet-only --version $VERSION

#

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS publish
ARG VERSION=0.0.1.65535-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY src/slskd src/slskd/.
COPY --from=web /slskd/src/web/build /slskd/src/slskd/wwwroot/.

RUN bash ./bin/publish --no-prebuild --runtime linux-musl-x64 --version $VERSION

#

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine AS slskd
ARG VERSION=0.0.1.65535-local

WORKDIR /slskd
COPY --from=publish /slskd/dist/linux-musl-x64 .

RUN mkdir /var/slsk
RUN mkdir /var/slsk/shared
RUN mkdir /var/slsk/download

ENV SLSK_OUTPUT_DIR=/var/slsk/download
ENV SLSK_SHARED_DIR=/var/slsk/shared

ENV SLSK_DOCKER_VERSION=${VERSION}
ENV SLSK_DOCKER_SHA=${SHA}

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["./slskd"]