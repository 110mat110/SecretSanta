# Build context: repository root
#   docker build -f SecretSanta.Web/Dockerfile -t secretsanta-web .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached while project files don't change)
COPY SecretSanta.ServiceDefaults/SecretSanta.ServiceDefaults.csproj SecretSanta.ServiceDefaults/
COPY SecretSanta.Web/SecretSanta.Web.csproj SecretSanta.Web/
RUN dotnet restore SecretSanta.Web/SecretSanta.Web.csproj

COPY SecretSanta.ServiceDefaults/ SecretSanta.ServiceDefaults/
COPY SecretSanta.Web/ SecretSanta.Web/
RUN dotnet publish SecretSanta.Web/SecretSanta.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# TLS is terminated by the hosting proxy (Render) – trust X-Forwarded-Proto so Google OAuth gets https:// redirect URIs
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app/publish .
USER $APP_UID

# Render provides the port in $PORT; fall back to 8080 elsewhere
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet SecretSanta.Web.dll"]
