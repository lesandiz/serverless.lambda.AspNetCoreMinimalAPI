FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore -r linux-x64 /p:PublishReadyToRun=true
# Build and publish a release
RUN dotnet publish -c Release -o /app/publish -r linux-x64 --self-contained true --no-restore /p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AspNetCoreMinimalAPI.dll"]