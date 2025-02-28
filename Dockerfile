#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["QuantumVault.Api/QuantumVault.Api.csproj", "QuantumVault.Api/"]
COPY ["QuantumVault.Core/QuantumVault.Core.csproj", "QuantumVault.Core/"]
COPY ["QuantumVault.Infrastructure/QuantumVault.Infrastructure.csproj", "QuantumVault.Infrastructure/"]

RUN dotnet restore "QuantumVault.Api/QuantumVault.Api.csproj"

# Copy the entire source code
COPY . .
WORKDIR "/src/QuantumVault.Api"

RUN dotnet build "QuantumVault.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "QuantumVault.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["certs/cert.pfx", "cert.pfx"] # Copy certificate to the container For Testing and Local Dev Only

# Switch to root user before installing packages
USER root

# Install necessary packages
RUN apt-get update && apt-get install -y ca-certificates \
    && update-ca-certificates

# Switch back to non-root user
USER app

ENTRYPOINT ["dotnet", "QuantumVault.Api.dll"]
