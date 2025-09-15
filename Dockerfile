# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Install CA certificates so HTTPS requests to CDNs (cdnjs) work
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy csproj files and restore dependencies
COPY src/RfcBuddy.Web/*.csproj ./src/RfcBuddy.Web/
COPY src/RfcBuddy.App/*.csproj ./src/RfcBuddy.App/
COPY src/RfcBuddy.App.Tests/*.csproj ./src/RfcBuddy.App.Tests/
RUN dotnet restore ./src/RfcBuddy.Web/RfcBuddy.Web.csproj

# Copy the rest of the source code
COPY . .

# Build and publish, no need for test
WORKDIR /app/src/RfcBuddy.Web
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:8080
ENV ASPNETCORE_ENVIRONMENT="Development"
ENV \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8
    
ENTRYPOINT ["dotnet", "RfcBuddy.Web.dll"]
