# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# curl is used by the final stage's HEALTHCHECK (docker-compose's api service has no
# other way to probe readiness without adding a package) — installed as root before
# dropping to $APP_UID below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
# 8081 is unused by the docker-compose path (the api service runs HTTP-only on 8080,
# see docker-compose.yml/Program.cs) — kept for Visual Studio's unrelated "Container
# (Dockerfile)" debug launch profile in Properties/launchSettings.json, which expects
# both ports.
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["API-server/ProductivityHub.csproj", "API-server/"]
RUN dotnet restore "./API-server/ProductivityHub.csproj"
COPY . .
WORKDIR "/src/API-server"
RUN dotnet build "./ProductivityHub.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ProductivityHub.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=5s --timeout=3s --start-period=15s --retries=10 \
    CMD curl -fsS http://127.0.0.1:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "ProductivityHub.dll"]