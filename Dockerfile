# syntax=docker/dockerfile:1

# ---- Stage 1: build. Uses the big SDK image, which is thrown away afterwards. ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy ONLY the project files first and restore. Docker caches each step, so as long as no .csproj changes,
# the slow package download is reused even when source code changes.
COPY Directory.Build.props ./
COPY src/GarageFlow.Domain/GarageFlow.Domain.csproj src/GarageFlow.Domain/
COPY src/GarageFlow.Application/GarageFlow.Application.csproj src/GarageFlow.Application/
COPY src/GarageFlow.Infrastructure/GarageFlow.Infrastructure.csproj src/GarageFlow.Infrastructure/
COPY src/GarageFlow.Api/GarageFlow.Api.csproj src/GarageFlow.Api/
RUN dotnet restore src/GarageFlow.Api/GarageFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/GarageFlow.Api/GarageFlow.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- Stage 2: run. Only the small ASP.NET runtime image plus the published files: no SDK, no source code. ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Port 8080 (not 80) so the app does not need root rights; "docker run -p 5080:8080" maps it outward.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the non-root user that Microsoft's image already contains. A bug in the app then cannot change the container.
USER $APP_UID

# Secrets are NOT baked in. They arrive as environment variables when the container starts:
#   ConnectionStrings__GarageFlow, Jwt__SigningKey (and Database__MigrateOnStartup=true to apply migrations).
ENTRYPOINT ["dotnet", "GarageFlow.Api.dll"]
