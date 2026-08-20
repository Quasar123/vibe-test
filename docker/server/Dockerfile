FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY VibeTest/VibeTest.Server/VibeTest.Server.csproj VibeTest/VibeTest.Server/
COPY VibeTest/vibetest.client/vibetest.client.esproj VibeTest/vibetest.client/
RUN dotnet restore VibeTest/VibeTest.Server/VibeTest.Server.csproj

COPY VibeTest/VibeTest.Server/ VibeTest/VibeTest.Server/
RUN dotnet publish VibeTest/VibeTest.Server/VibeTest.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "VibeTest.Server.dll"]
