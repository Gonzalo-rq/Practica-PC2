#!/bin/sh
# entrypoint.sh: Asegura la expansión dinámica de la variable PORT proporcionada por Render
PORT="${PORT:-8080}"
echo "Iniciando CreditosApp en el puerto: $PORT"
exec dotnet CreditosApp.dll --urls "http://0.0.0.0:${PORT}"
