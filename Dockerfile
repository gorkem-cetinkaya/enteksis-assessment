# Build stage: restore and publish the web project with the .NET 10 SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer stays cached when only source files change.
COPY src/ServiceRequests.Web/ServiceRequests.Web.csproj src/ServiceRequests.Web/
RUN dotnet restore src/ServiceRequests.Web/ServiceRequests.Web.csproj

COPY src/ServiceRequests.Web/ src/ServiceRequests.Web/
RUN dotnet publish src/ServiceRequests.Web/ServiceRequests.Web.csproj \
    --configuration Release --no-restore --output /app/publish

# Runtime stage: only the ASP.NET Core runtime and the published output.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# tini (from the Ubuntu package repository) runs as PID 1: it forwards
# signals such as SIGTERM to dotnet and passes on its exit code.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Listen on 0.0.0.0:8080 (the image default, set explicitly here).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the image's built-in non-root user.
USER $APP_UID
ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "ServiceRequests.Web.dll"]
