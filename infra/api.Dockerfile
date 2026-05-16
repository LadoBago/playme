# Multi-stage Dockerfile for the PlayMe API.
# CLAUDE.md §6: ship from day one for local/cloud parity.
#
# Build context is the repo root:
#   docker build -f infra/api.Dockerfile -t playme-api .

# ─── build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (cached layer) — copy only project files first. Restore the Api
# project directly: it transitively pulls in Application/Domain/Infrastructure,
# while the slnx also lists the test project (not needed in the prod image
# and intentionally not COPY'd here).
COPY apps/api/Directory.Build.props apps/api/Directory.Packages.props ./apps/api/
COPY apps/api/src/PlayMe.Domain/PlayMe.Domain.csproj ./apps/api/src/PlayMe.Domain/
COPY apps/api/src/PlayMe.Application/PlayMe.Application.csproj ./apps/api/src/PlayMe.Application/
COPY apps/api/src/PlayMe.Infrastructure/PlayMe.Infrastructure.csproj ./apps/api/src/PlayMe.Infrastructure/
COPY apps/api/src/PlayMe.Api/PlayMe.Api.csproj ./apps/api/src/PlayMe.Api/
COPY global.json ./
RUN dotnet restore apps/api/src/PlayMe.Api/PlayMe.Api.csproj

# Copy the rest and publish.
COPY apps/api/ ./apps/api/
RUN dotnet publish apps/api/src/PlayMe.Api/PlayMe.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── runtime stage ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Don't run as root. `--create-home` is kept as a belt-and-braces fallback:
# ASP.NET Core Data Protection now persists keys to Redis (configured in
# AddApi.cs), but historically it tried to write under $HOME/.aspnet/
# DataProtection-Keys on the first cookie mint and 500'd with "Access to
# the path '/home/playme' is denied" when the home didn't exist. The
# Redis-backed XmlRepository should make that path unreachable, but a
# writable home is cheap and protects against any future framework
# defaulting that bypasses our IXmlRepository.
RUN useradd --create-home --shell /usr/sbin/nologin --uid 10001 playme
USER playme

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PlayMe.Api.dll"]
