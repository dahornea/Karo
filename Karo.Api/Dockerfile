# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY Karo.slnx ./
COPY Karo.Api/Karo.Api.csproj Karo.Api/
RUN dotnet restore Karo.Api/Karo.Api.csproj --configfile NuGet.Config

COPY Karo.Api/ Karo.Api/
RUN dotnet publish Karo.Api/Karo.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Karo.Api.dll"]
