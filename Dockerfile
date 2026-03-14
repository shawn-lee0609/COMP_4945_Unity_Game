# Use .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy Server project files
COPY Server/*.csproj ./Server/
WORKDIR /app/Server
RUN dotnet restore

# Copy everything else and build
WORKDIR /app
COPY Server/ ./Server/
WORKDIR /app/Server
RUN dotnet publish -c Release -o out

# Use runtime image for deployment
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/Server/out .

# Expose port (Railway assigns this dynamically)
EXPOSE 8080

# Set environment variable for ASP.NET to listen on Railway's port
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}

# Start the server
ENTRYPOINT ["dotnet", "Server.dll"]