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

# Crear directorio para persistencia opcional de base de datos SQLite en disco de Render
RUN mkdir -p /data

COPY --from=build /app/publish .
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/data/app.db;Cache=Shared"

ENTRYPOINT ["/app/entrypoint.sh"]
