# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CreditosApp.csproj", "./"]
RUN dotnet restore "CreditosApp.csproj"

COPY . .
RUN dotnet publish "CreditosApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Crear directorios con permisos para SQLite tanto en /data como en /var/data
RUN mkdir -p /data /var/data && chmod -R 777 /data /var/data

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/data/app.db;Cache=Shared"

# Comando de inicio con expansión directa de PORT en Linux shell sin depender de scripts externos
ENTRYPOINT ["sh", "-c", "exec dotnet CreditosApp.dll --urls http://0.0.0.0:${PORT:-8080}"]
