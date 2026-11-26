# Build from repository root:
# docker build -f src/RFiDGear.Builder.SyncServer/Dockerfile -t sieencode-sync:dev .
# Resolve and pin base image digests in approved SieTask CI before release.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props NuGet.Config ./
COPY src/RFiDGear.Builder.Core/*.csproj src/RFiDGear.Builder.Core/
COPY src/RFiDGear.Builder.SyncServer/*.csproj src/RFiDGear.Builder.SyncServer/
RUN dotnet restore src/RFiDGear.Builder.SyncServer/RFiDGear.Builder.SyncServer.csproj
COPY src/RFiDGear.Builder.Core/*.cs src/RFiDGear.Builder.Core/
COPY src/RFiDGear.Builder.SyncServer/*.cs src/RFiDGear.Builder.SyncServer/
RUN dotnet publish src/RFiDGear.Builder.SyncServer/RFiDGear.Builder.SyncServer.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out/ ./
ENV SIEENCODE_SYNC_HOSTING=fargate \
    SIEENCODE_SYNC_START_MODE=existing \
    SIEENCODE_SYNC_STATE_FILE=/var/lib/sieencode/live/central-state.json \
    SIEENCODE_SYNC_PORT=8443 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8443
USER $APP_UID
ENTRYPOINT ["dotnet", "RFiDGear.Builder.SyncServer.dll"]
