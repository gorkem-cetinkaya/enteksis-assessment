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
WORKDIR /app
COPY --from=build /app/publish .

# Run as the image's built-in non-root user. ASP.NET Core images listen on 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ServiceRequests.Web.dll"]
