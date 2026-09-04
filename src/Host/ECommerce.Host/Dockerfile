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

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Never run as root.
RUN adduser --system --group --no-create-home catalog
USER catalog

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# FR-036 / research.md R13 — liveness only. Readiness is the load balancer's concern and
# deliberately excludes the Promotion module, so it must not gate the container's own health.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD ["/bin/sh", "-c", "wget -qO- http://localhost:8080/health/live || exit 1"]

ENTRYPOINT ["dotnet", "ECommerce.Host.dll"]
