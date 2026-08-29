# NFR-08: двухэтапная сборка. NFR-09: Linux-контейнер.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы описания сборки — слой restore переиспользуется,
# пока не менялись зависимости.
COPY Directory.Build.props Directory.Packages.props NuGet.config ./
COPY src/CentralWikiMcp.Domain/*.csproj src/CentralWikiMcp.Domain/
COPY src/CentralWikiMcp.Infrastructure/*.csproj src/CentralWikiMcp.Infrastructure/
COPY src/CentralWikiMcp.Server/*.csproj src/CentralWikiMcp.Server/
COPY src/CentralWikiMcp.StdioProxy/*.csproj src/CentralWikiMcp.StdioProxy/
COPY src/stubs/Abdt.Infrastructure.Configuration.Validation/*.csproj src/stubs/Abdt.Infrastructure.Configuration.Validation/
COPY src/stubs/Abdt.Infrastructure.Configuration.Vault/*.csproj src/stubs/Abdt.Infrastructure.Configuration.Vault/
COPY src/stubs/Abdt.Infrastructure.Logging.AspNetCore/*.csproj src/stubs/Abdt.Infrastructure.Logging.AspNetCore/
COPY src/stubs/Abdt.Infrastructure.Monitoring.HealthCheck/*.csproj src/stubs/Abdt.Infrastructure.Monitoring.HealthCheck/
COPY src/stubs/Abdt.Infrastructure.OpenTelemetry.Metrics/*.csproj src/stubs/Abdt.Infrastructure.OpenTelemetry.Metrics/
COPY src/stubs/Abdt.Infrastructure.RateLimiting/*.csproj src/stubs/Abdt.Infrastructure.RateLimiting/
COPY src/stubs/Protoobp.Trace.Bundle/*.csproj src/stubs/Protoobp.Trace.Bundle/

RUN dotnet restore src/CentralWikiMcp.Server/CentralWikiMcp.Server.csproj

COPY src/ src/
RUN dotnet publish src/CentralWikiMcp.Server/CentralWikiMcp.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl нужен только для HEALTHCHECK: в базовом образе его нет.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

# NFR-36: процесс не должен работать от root.
RUN useradd --uid 64198 --create-home --shell /usr/sbin/nologin wikimcp
USER wikimcp

COPY --from=build --chown=wikimcp:wikimcp /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1

EXPOSE 8080

# FR-07: liveness-проверка контейнера бьёт в тот же эндпоинт, что и k8s-проба.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CentralWikiMcp.Server.dll"]
