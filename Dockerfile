FROM node:lts-alpine3.13 AS web
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

FROM mcr.microsoft.com/dotnet/sdk:5.0.103-focal-amd64 AS publish
ARG TARGETPLATFORM
ARG VERSION=0.0.1.65534-local

WORKDIR /slskd

COPY LICENSE .
COPY bin bin/.
COPY src/slskd src/slskd/.
COPY --from=web /slskd/src/web/build /slskd/src/slskd/wwwroot/.

RUN bash ./bin/publish --no-prebuild --platform $TARGETPLATFORM --version $VERSION --output ../../dist/${TARGETPLATFORM}

#

FROM alpine:3.13 AS slskd
ARG TARGETPLATFORM
ARG VERSION=0.0.1.65534-local

LABEL org.opencontainers.image.source=https://github.com/slskd/slskd
LABEL org.opencontainers.image.licsense=AGPL-3.0
LABEL org.opencontainers.image.version=${VERSION}

# the following is a 1:1 copy of the .NET 5 runtime-deps dockerfile, which does not yet support alpine/armv7
# review this in the future to determine if we can return to using mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine as the base
# https://github.com/dotnet/dotnet-docker/blob/56e04001eb08a0399c8887a517f2dc9a81dcdf04/src/runtime-deps/5.0/alpine3.13/amd64/Dockerfile
RUN apk add --no-cache \
        ca-certificates \
        \
        # .NET Core dependencies
        krb5-libs \
        libgcc \
        libintl \
        libssl1.1 \
        libstdc++ \
        zlib

ENV \
    # Configure web servers to bind to port 80 when present
    ASPNETCORE_URLS=http://+:80 \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Set the invariant mode since icu_libs isn't included (see https://github.com/dotnet/announcements/issues/20)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
# end runtime-deps

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