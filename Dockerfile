FROM node:lts-alpine3.12 AS web
WORKDIR /web/
COPY src/web .
RUN npm ci
RUN npm run test-unattended
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:5.0 as build
WORKDIR /slskd/
COPY src/slskd .
COPY --from=web web/build wwwroot
RUN dotnet restore
RUN dotnet build --no-restore --configuration Release
RUN dotnet test --no-build --verbosity minimal --blame

FROM mcr.microsoft.com/dotnet/sdk:5.0 as publish
WORKDIR /slskd/
COPY src/slskd .
COPY --from=web web/build wwwroot
RUN dotnet publish --configuration Release -p:PublishSingleFile=true -p:ReadyToRun=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true -p:CopyOutputSymbolsToPublishDirectory=false --self-contained --runtime linux-musl-x64 --output dist

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine
WORKDIR /slskd/
COPY --from=publish slskd/dist .

RUN mkdir /var/slsk
RUN mkdir /var/slsk/shared
RUN mkdir /var/slsk/download

ENV SLSK_OUTPUT_DIR=/var/slsk/download
ENV SLSK_SHARED_DIR=/var/slsk/shared

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["./slskd"]