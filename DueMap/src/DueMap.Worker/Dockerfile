# syntax=docker/dockerfile:1.7
#
# DueMap.Worker. Same multi-stage pattern as Web.
#
# The Worker depends on Microsoft.AspNetCore.DataProtection (to read OAuth
# tokens written by Web), so it uses the ASP.NET runtime base image — not
# the slimmer "runtime" image.
#
# Usage from solution root:
#   docker build -f src/DueMap.Worker/Dockerfile -t duemap-worker:dev .

ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION
WORKDIR /src

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

RUN dotnet restore "src/DueMap.Worker/DueMap.Worker.csproj"

COPY . .
RUN dotnet publish "src/DueMap.Worker/DueMap.Worker.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DueMap.Worker.dll"]
