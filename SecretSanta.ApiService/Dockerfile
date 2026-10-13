# Build context: repository root
#   docker build -f SecretSanta.ApiService/Dockerfile -t secretsanta-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached while project files don't change)
COPY SecretSanta.ServiceDefaults/SecretSanta.ServiceDefaults.csproj SecretSanta.ServiceDefaults/
COPY SecretSanta.ApiService/SecretSanta.ApiService.csproj SecretSanta.ApiService/
RUN dotnet restore SecretSanta.ApiService/SecretSanta.ApiService.csproj

COPY SecretSanta.ServiceDefaults/ SecretSanta.ServiceDefaults/
COPY SecretSanta.ApiService/ SecretSanta.ApiService/
RUN dotnet publish SecretSanta.ApiService/SecretSanta.ApiService.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app/publish .
USER $APP_UID

# Render provides the port in $PORT; fall back to 8080 elsewhere
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet SecretSanta.ApiService.dll"]
