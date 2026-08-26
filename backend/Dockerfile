# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia só os csproj primeiro para aproveitar cache de "dotnet restore"
COPY src/Pacus.Domain/Pacus.Domain.csproj src/Pacus.Domain/
COPY src/Pacus.Application/Pacus.Application.csproj src/Pacus.Application/
COPY src/Pacus.Infrastructure/Pacus.Infrastructure.csproj src/Pacus.Infrastructure/
COPY src/Pacus.Api/Pacus.Api.csproj src/Pacus.Api/

RUN dotnet restore src/Pacus.Api/Pacus.Api.csproj

# Copia o resto do código e publica
COPY src/ src/
RUN dotnet publish src/Pacus.Api/Pacus.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render injeta a variavel de ambiente PORT em runtime; o Kestrel precisa
# escutar nela. Usamos forma shell no ENTRYPOINT para expandir $PORT
# (com fallback 8080 para rodar local via "docker run").
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet Pacus.Api.dll"]
