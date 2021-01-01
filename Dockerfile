FROM node:lts-alpine3.12 AS web
WORKDIR /web/
COPY src/web .
RUN npm install
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:5.0 as build
WORKDIR /slskd/
COPY src/slskd .
COPY --from=web web/build wwwroot
RUN dotnet publish --configuration Release -p:PublishSingleFile=true -p:ReadyToRun=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained --runtime linux-musl-x64

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-alpine
WORKDIR /slskd/
COPY --from=build slskd/bin/Release/net5.0/linux-musl-x64/publish .

RUN mkdir /var/slsk
RUN mkdir /var/slsk/shared
RUN mkdir /var/slsk/download

ENV SLSK_OUTPUT_DIR=/var/slsk/download
ENV SLSK_SHARED_DIR=/var/slsk/shared

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["./slskd"]