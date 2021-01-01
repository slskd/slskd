FROM mcr.microsoft.com/dotnet/aspnet:5.0

COPY src/slskd/bin/Release/net5.0/publish app/

RUN mkdir /var/slsk
RUN mkdir /var/slsk/shared
RUN mkdir /var/slsk/download

ENV SLSK_OUTPUT_DIR=/var/slsk/download
ENV SLSK_SHARED_DIR=/var/slsk/shared

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "app/slskd.dll"]