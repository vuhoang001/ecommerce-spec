# DEP-001 — every deployable ships as a container image built by CI from a checked-in
# Dockerfile. Build or install steps performed OUTSIDE the image are FORBIDDEN, so this file
# restores and publishes inside the build stage rather than copying artifacts from the runner.
#
# DEP-002 — this image builds from the backend source tree alone. It requires no frontend
# toolchain and no frontend source, so the two deployables are independently releasable.
#
# Build from the repository root:
#   docker build -f src/Host/ECommerce.Host/Dockerfile -t ecommerce-host:local .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# global.json pins the SDK feature band; copy it first so a mismatch fails immediately rather
# than after a long restore.
COPY global.json ./
COPY Directory.Build.props Directory.Packages.props ECommerce.sln ./

# Project files first: this layer changes only when a dependency changes, so an ordinary source
# edit reuses the restore cache.
COPY src/Shared/ECommerce.Shared.Kernel/*.csproj                          src/Shared/ECommerce.Shared.Kernel/
COPY src/Shared/ECommerce.Shared.Messaging/*.csproj                       src/Shared/ECommerce.Shared.Messaging/
COPY src/Modules/Catalog/ECommerce.Catalog.Contracts/*.csproj             src/Modules/Catalog/ECommerce.Catalog.Contracts/
COPY src/Modules/Catalog/ECommerce.Catalog.Domain/*.csproj                src/Modules/Catalog/ECommerce.Catalog.Domain/
COPY src/Modules/Catalog/ECommerce.Catalog.Application/*.csproj           src/Modules/Catalog/ECommerce.Catalog.Application/
COPY src/Modules/Catalog/ECommerce.Catalog.Infrastructure/*.csproj        src/Modules/Catalog/ECommerce.Catalog.Infrastructure/
COPY src/Modules/Promotion/ECommerce.Promotion.Contracts/*.csproj         src/Modules/Promotion/ECommerce.Promotion.Contracts/
COPY src/Host/ECommerce.Host/*.csproj                                     src/Host/ECommerce.Host/

RUN dotnet restore src/Host/ECommerce.Host/ECommerce.Host.csproj

COPY src/ src/
RUN dotnet publish src/Host/ECommerce.Host/ECommerce.Host.csproj \
        -c Release -o /app/publish --no-restore

# Alpine, specifically: the Debian-based aspnet:8.0 image contains neither wget nor curl, so the
# HEALTHCHECK below could never succeed. The container reported `unhealthy` while serving 200s,
# and anything reading Docker health -- depends_on: service_healthy, restart policies, deploy
# gates -- was acting on a permanently false signal. Alpine's busybox provides wget.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Never run as root. busybox adduser takes different flags from the Debian one.
RUN addgroup -S catalog && adduser -S -G catalog catalog
USER catalog

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# FR-036 / research.md R13 — liveness only. Readiness is the load balancer's concern and
# deliberately excludes the Promotion module, so it must not gate the container's own health.
# Liveness only. Readiness is the load balancer's concern and deliberately excludes the
# Promotion module (research.md R13); gating the container's own health on it would restart a
# perfectly healthy process during a Promotion outage.
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD wget --quiet --spider --tries=1 http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ECommerce.Host.dll"]
