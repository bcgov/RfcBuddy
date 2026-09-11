# Build stage
# .NET 10.0 manifest digest resolved 2026-09-11.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:60a2b2230a0d052bc54c0d453e97e331219ed503c5411470ee226859420e693c AS build
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

# Copy individual folders specifically rather than recursively copying everything
COPY src/ ./src/
COPY CHANGELOG.md LICENSE README.md RfcBuddy.sln ./

# Build and publish, no need for test
WORKDIR /app/src/RfcBuddy.Web
RUN dotnet publish -c Release -o /out

# Runtime stage
# .NET 10.0 manifest digest resolved 2026-09-11.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:900c2dd83cc0cef53db0aaf786f12fe766ee075b6334750a664c9e77e7a7c0c5 AS runtime
WORKDIR /app
COPY --from=build /out ./

RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Run as a non-privileged system user for security best practices
USER 1001

VOLUME /app/data

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV \
    LC_ALL=C.UTF-8 \
    LANG=C.UTF-8
    
ENTRYPOINT ["dotnet", "RfcBuddy.Web.dll"]
