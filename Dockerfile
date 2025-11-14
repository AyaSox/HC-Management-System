# Use the official .NET SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Use the HRManagementSystem folder as the Docker build context in Render
# so these COPY paths are correct.
COPY HRManagementSystem/HRManagementSystem.csproj ./
RUN dotnet restore HRManagementSystem.csproj

# Copy the rest of the HR app
COPY . .

# Publish
RUN dotnet publish -c Release -o /app/out --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HRManagementSystem.dll"]
