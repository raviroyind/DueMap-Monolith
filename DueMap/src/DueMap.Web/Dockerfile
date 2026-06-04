# syntax=docker/dockerfile:1.7
#
# Multi-stage build for DueMap.Web. Build context is the solution root (the
# directory containing DueMap.sln) — necessary for restore to see all the
# referenced .csproj files.
#
# Usage from solution root:
#   docker build -f src/DueMap.Web/Dockerfile -t duemap-web:dev .

ARG BUILD_CONFIGURATION=Release

# --------------------------------------------------------------- restore + build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION
WORKDIR /src

# Copy only the project files first so `dotnet restore` can be cached
# independently of source changes.
COPY ["DueMap.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/DueMap.Common/DueMap.Common.csproj",                            "src/DueMap.Common/"]
COPY ["src/DueMap.Web/DueMap.Web.csproj",                                  "src/DueMap.Web/"]
COPY ["src/DueMap.Worker/DueMap.Worker.csproj",                            "src/DueMap.Worker/"]
COPY ["src/Modules/DueMap.Tenancy/DueMap.Tenancy.csproj",                  "src/Modules/DueMap.Tenancy/"]
COPY ["src/Modules/DueMap.Rules/DueMap.Rules.csproj",                      "src/Modules/DueMap.Rules/"]
COPY ["src/Modules/DueMap.Notices/DueMap.Notices.csproj",                  "src/Modules/DueMap.Notices/"]
COPY ["src/Modules/DueMap.Billing/DueMap.Billing.csproj",                  "src/Modules/DueMap.Billing/"]
COPY ["src/Modules/DueMap.Integrations/DueMap.Integrations.csproj",        "src/Modules/DueMap.Integrations/"]
COPY ["src/Modules/DueMap.Identity/DueMap.Identity.csproj",                "src/Modules/DueMap.Identity/"]

RUN dotnet restore "src/DueMap.Web/DueMap.Web.csproj"

# Bring in the rest of the source and publish.
COPY . .
RUN dotnet publish "src/DueMap.Web/DueMap.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# $APP_UID is a non-root user pre-created by the official images.
USER $APP_UID

COPY --from=build /app/publish .

# Default port; override via ASPNETCORE_URLS if needed.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DueMap.Web.dll"]
