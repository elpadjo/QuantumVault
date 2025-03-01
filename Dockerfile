#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["QuantumVault.Api/QuantumVault.Api.csproj", "QuantumVault.Api/"]
COPY ["QuantumVault.Core/QuantumVault.Core.csproj", "QuantumVault.Core/"]
RUN dotnet restore "./QuantumVault.Api/QuantumVault.Api.csproj"
COPY . .
WORKDIR "/src/QuantumVault.Api"
RUN dotnet build "./QuantumVault.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./QuantumVault.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Switch to root to create and set permissions on /storage
USER root
RUN mkdir -p /storage && chown -R app:app /storage

# Switch back to non-root user
USER app

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuantumVault.Api.dll"]