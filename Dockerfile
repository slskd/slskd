FROM node:lts-alpine3.12 AS web
WORKDIR /slskd
COPY src/web src/web/.
WORKDIR /slskd/src/web
RUN npm ci
RUN npm run test-unattended
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:5.0 as build
ARG VERSION=0.0.1
ARG SHA=local

WORKDIR /slskd
COPY LICENSE .
COPY src/slskd src/slskd/.
COPY tests tests/.

WORKDIR /slskd/src/slskd
COPY --from=web /slskd/src/web/build wwwroot/.
RUN dotnet build --configuration Release -p:Version=$VERSION-$SHA

WORKDIR /slskd/tests
RUN dotnet test --configuration Release slskd.Tests.Unit
RUN dotnet test --configuration Release slskd.Tests.Integration

FROM mcr.microsoft.com/dotnet/sdk:5.0 as publish
ARG VERSION=0.0.1
ARG SHA=local

WORKDIR /slskd
COPY LICENSE .
COPY src/slskd src/slskd/.
WORKDIR /slskd/src/slskd
COPY --from=web /slskd/src/web/build wwwroot/.
RUN dotnet publish --configuration Release -p:PublishSingleFile=true -p:ReadyToRun=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true -p:CopyOutputSymbolsToPublishDirectory=false -p:Version=$VERSION-$SHA --self-contained --runtime linux-musl-x64 --output ../../dist

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine
WORKDIR /slskd
COPY --from=publish /slskd/dist .

RUN mkdir /var/slsk
RUN mkdir /var/slsk/shared
RUN mkdir /var/slsk/download

ENV SLSK_OUTPUT_DIR=/var/slsk/download
ENV SLSK_SHARED_DIR=/var/slsk/shared

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["./slskd"]