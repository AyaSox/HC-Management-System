# Simplified Dockerfile for ESS Leave System - Render deployment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects and restore (paths relative to repo root where Dockerfile is executed)
COPY HRManagement.Shared/HRManagement.Shared.csproj HRManagement.Shared/
COPY HRManagementSystem/ESSLeaveSystem/ESSLeaveSystem.csproj HRManagementSystem/ESSLeaveSystem/
RUN dotnet restore HRManagementSystem/ESSLeaveSystem/ESSLeaveSystem.csproj

# Copy source and build
COPY HRManagement.Shared/ HRManagement.Shared/
COPY HRManagementSystem/ESSLeaveSystem/ HRManagementSystem/ESSLeaveSystem/
WORKDIR /src/HRManagementSystem/ESSLeaveSystem
RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Simple configuration
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ESSLeaveSystem.dll"]