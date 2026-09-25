# Plataforma de Gestión de Solicitudes de Crédito (.NET 10 MVC)

Sistema web interno para una entidad financiera orientado a la gestión y evaluación de solicitudes de crédito, desarrollado con **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core (SQLite)**, **Identity**, caché distribuido y sesiones con **Redis**, notificaciones en tiempo real vía **WebSocket (SignalR)**, y arquitectura de mensajería asíncrona mediante **Cloud MQ (RabbitMQ en CloudAMQP)**.

Desplegado en **Render.com** como Web Service en contenedor Docker.

---

## 📌 Tabla de Contenidos
1. [Arquitectura y Tecnologías](#-arquitectura-y-tecnologías)
2. [Cuentas de Prueba Preconfiguradas (Seed Data)](#-cuentas-de-prueba-preconfiguradas-seed-data)
3. [Instrucciones de Ejecución Local](#-instrucciones-de-ejecución-local)
4. [Variables de Entorno](#-variables-de-entorno)
5. [Estrategia de Persistencia de SQLite en Render](#-estrategia-de-persistencia-de-sqlite-en-render)
6. [Guía de Pruebas y Evidencias por Pregunta](#-guía-de-pruebas-y-evidencias-por-pregunta)
   - [Pregunta 1 & 2: Dominio, Catálogo y Filtros](#pregunta-1--2-dominio-catálogo-y-filtros)
   - [Pregunta 3 & 5: Reglas de Negocio y Panel de Analista](#pregunta-3--5-reglas-de-negocio-y-panel-de-analista)
   - [Pregunta 4: Sesión y Caché Redis](#pregunta-4-sesión-y-caché-redis)
   - [Pregunta 6: Notificaciones en Tiempo Real con WebSocket](#pregunta-6-notificaciones-en-tiempo-real-con-websocket)
   - [Pregunta 7: Mensajería Asíncrona con Cloud MQ](#pregunta-7-mensajería-asíncrona-con-cloud-mq)
7. [Despliegue en Render (Pregunta 8)](#-despliegue-en-render-pregunta-8)
8. [Historial de Ramas y Pull Requests en GitHub](#-historial-de-ramas-y-pull-requests-en-github)

---

## 🚀 Arquitectura y Tecnologías

- **Framework**: .NET 10.0 (ASP.NET Core MVC con Razor Views).
- **ORM & Base de Datos**: Entity Framework Core 10 con SQLite (`app.db`), índice único filtrado para restringir una sola solicitud pendiente por cliente.
- **Autenticación y Roles**: ASP.NET Core Identity con rol `Analista` y políticas `[Authorize(Roles = "Analista")]`.
- **Memoria Temporal / Caché**: Redis gestionado (Redis Cloud) para caché de 60 segundos con invalidación proactiva y almacenamiento de sesión HTTP.
- **Tiempo Real**: ASP.NET Core SignalR Hub en `/hubs/solicitudes`, transporte forzado por WebSocket, autenticación por Identity y reconexión automática.
- **Broker de Mensajería**: RabbitMQ administrado en CloudAMQP con protocolo seguro `amqps://`, Publisher Confirms, cola durable `solicitudes.notificaciones`, ACK manual e idempotencia garantizada por UUID (`MessageId`).
- **Contenedores**: Dockerfile multi-stage basado en `mcr.microsoft.com/dotnet/aspnet:10.0` con script de inicio `entrypoint.sh` para expansión dinámica de `$PORT`.

---

## 👥 Cuentas de Prueba Preconfiguradas (Seed Data)

El sistema inicializa automáticamente la base de datos al arrancar con las siguientes credenciales:

| Rol | Correo Electrónico | Contraseña | Ingresos Mensuales | Solicitud Inicial |
| :--- | :--- | :--- | :--- | :--- |
| **Analista de Riesgo** | `analista@creditos.com` | `Analista123!` | N/A | Acceso a `/Analista` |
| **Cliente 1** | `cliente1@creditos.com` | `Cliente123!` | $3,000.00 | Solicitud #1: $5,000.00 (**Pendiente**) |
| **Cliente 2** | `cliente2@creditos.com` | `Cliente123!` | $4,500.00 | Solicitud #2: $10,000.00 (**Aprobado**) |

---

## 💻 Instrucciones de Ejecución Local

### 1. Clonar el Repositorio
```bash
git clone git@github.com:Gonzalo-rq/Practica-PC2.git
cd Practica-PC2
```

### 2. Restaurar Paquetes NuGet
```bash
dotnet restore
```

### 3. Aplicar Migraciones a SQLite
```bash
dotnet ef database update
```
*(Al iniciar la aplicación por primera vez, `DbInitializer` sembrará automáticamente los roles, usuarios y solicitudes iniciales).*

### 4. Ejecutar la Aplicación
```bash
dotnet run
```
Abre tu navegador en la URL indicada (habitualmente `http://localhost:5000` o `https://localhost:5001`).

---

## ⚙️ Variables de Entorno

Para ejecutar tanto en local como en producción (Render), se utilizan las siguientes variables de configuración:

| Variable | Descripción | Valor de Ejemplo |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución de ASP.NET Core | `Production` |
| `ConnectionStrings__DefaultConnection` | Conexión a SQLite | `Data Source=/var/data/app.db;Cache=Shared` |
| `Redis__ConnectionString` | Host, puerto, credenciales de Redis Cloud | `superglossy-hobbies-tangerine-38114.db.redis.io:13512,password=...,ssl=False` |
| `RabbitMq__ConnectionString` | URI AMQPS de CloudAMQP | `amqps://usuario:password@jackal.rmq.cloudamqp.com/vhost` |
| `RabbitMq__QueueName` | Nombre de la cola durable de notificaciones | `solicitudes.notificaciones` |
| `RabbitMq__ConsumerEnabled` | Controla si el BackgroundService procesa mensajes | `true` (o `false` para pruebas) |
| `PORT` | Puerto asignado dinámicamente por Render | `10000` (definido por Render) |

---

## 💾 Estrategia de Persistencia de SQLite en Render

Dado que los contenedores en Render utilizan un sistema de archivos efímero por defecto (los datos locales se borran al reiniciar o volver a desplegar), se implementa la siguiente solución de persistencia:

1. **Disco Persistente de Render (Render Disks)**:
   - Se crea un disco persistente (ej. de 1 GB) montado en la ruta `/var/data` (especificado en `render.yaml`).
   - La cadena de conexión se configura apuntando a dicha ruta:
     ```env
     ConnectionStrings__DefaultConnection=Data Source=/var/data/app.db;Cache=Shared
     ```
2. **Ciclo de Migraciones Automáticas**:
   - `DbInitializer.InitializeAsync(scope.ServiceProvider)` invoca `context.Database.MigrateAsync()` al levantar el servicio, garantizando que si se crea una nueva instancia con un disco vacío, la base de datos se genera y siembra automáticamente.
3. **Expansión de `$PORT` en Docker**:
   - Render no expande `${PORT}` dentro de variables de entorno de Docker. Por tal motivo se diseñó `entrypoint.sh`:
     ```sh
     #!/bin/sh
     PORT="${PORT:-8080}"
     exec dotnet CreditosApp.dll --urls "http://0.0.0.0:${PORT}"
     ```

---

## 🧪 Guía de Pruebas y Evidencias por Pregunta

### Pregunta 1 & 2: Dominio, Catálogo y Filtros
1. Inicia sesión con `cliente1@creditos.com` / `Cliente123!`.
2. Dirígete a **Mis Solicitudes** (`/Solicitudes`).
3. Comprueba el listado de solicitudes del usuario.
4. **Filtros en servidor**:
   - Filtra por estado `Pendiente`.
   - Ingresa un monto mínimo negativo (ej. `-100`): comprueba que el servidor rechaza la solicitud mostrando el error correspondiente.
   - Ingresa un rango de fechas con fecha inicial posterior a fecha final: comprueba la validación en servidor.
5. Haz clic en **Ver Detalle** para examinar la información completa de la solicitud.

### Pregunta 3 & 5: Reglas de Negocio y Panel de Analista
1. Con `cliente1@creditos.com`, dirígete a **Nueva Solicitud** (`/Solicitudes/Crear`).
   - Dado que `cliente1` ya tiene una solicitud pendiente, el sistema deshabilita el formulario e indica que no es posible registrar otra.
2. Inicia sesión en una ventana de incógnito como `analista@creditos.com` / `Analista123!`.
3. Entra a **Panel Analista** (`/Analista`):
   - Se visualizan todas las solicitudes pendientes.
   - **Regla de Riesgo (Máximo 5x ingresos)**: Si una solicitud supera 5 veces los ingresos, el analista no puede aprobarla.
   - **Rechazo**: Al hacer clic en *Rechazar*, se abre un modal solicitando obligatoriamente el motivo de rechazo.
   - **Aprobación**: Aprueba la solicitud #1 de `cliente1`.

### Pregunta 4: Sesión y Caché Redis
1. **Última Solicitud en Sesión**:
   - Al entrar al detalle de cualquier solicitud, observa en la barra de navegación superior el botón: `Ver última solicitud ($X.XX)`.
   - Navega a otra sección y comprueba que dicho enlace persiste gracias a la sesión respaldada en Redis.
2. **Caché Distribuido (60s)**:
   - Al acceder a *Mis Solicitudes* sin filtros, el sistema guarda el listado en Redis con clave `solicitudes_usuario_{userId}`.
   - En la vista se muestra la etiqueta: `Fuente de datos: Redis Cache (60s)`.
   - Al registrar una nueva solicitud o al ser evaluada por un analista, la caché se invalida inmediatamente.

### Pregunta 6: Notificaciones en Tiempo Real con WebSocket
1. Abre dos navegadores o ventanas en paralelo:
   - Ventana A: `cliente1@creditos.com` en la vista **Mis Solicitudes**.
   - Ventana B: `analista@creditos.com` en el **Panel Analista** (`/Analista`).
2. En la Ventana B, evalúa la solicitud de `cliente1` (Aprobar o Rechazar).
3. **Resultado Inmediato**:
   - En la Ventana A (`cliente1`), sin recargar la página, aparece un Toast/alerta emergente y el badge de la tabla cambia instantáneamente de color y estado.
4. **Prueba de Aislamiento**:
   - Abre un tercer navegador con `cliente2@creditos.com`. Al aprobar la solicitud de `cliente1`, `cliente2` no recibe ningún aviso.
5. **Prueba de Conexión Anónima**:
   - En una ventana privada sin autenticar, intenta acceder mediante consola a `new WebSocket("wss://<host>/hubs/solicitudes")`; el servidor responderá con `HTTP 401 Unauthorized`.

### Pregunta 7: Mensajería Asíncrona con Cloud MQ
1. **Desactivar consumidor**:
   - Configura `RabbitMq__ConsumerEnabled=false` en variables de entorno o `appsettings.json` y arranca la aplicación.
   - Inicia sesión como cliente y crea una nueva solicitud de crédito válida.
   - Entra al dashboard de CloudAMQP: en la cola `solicitudes.notificaciones` verás **1 mensaje listo (Ready)** esperando ser consumido.
2. **Reactivar consumidor y confirmar (ACK)**:
   - Cambia a `RabbitMq__ConsumerEnabled=true` y reinicia el servicio.
   - Observa en los logs cómo el `BackgroundService` toma el mensaje, lo almacena en la tabla `Notificaciones` y emite el `BasicAck` manual. La cola en CloudAMQP queda en 0.
   - Entra a **Mis Notificaciones** (`/Notificaciones`) y confirma la aparición del mensaje: *"Recibimos tu solicitud de crédito y está pendiente de evaluación"*.
3. **Prueba de Idempotencia**:
   - Si se reenviara un mensaje con el mismo `MessageId` (UUID), el consumidor detecta que ya existe en la base de datos y envía el `BasicAck` sin registrar un duplicado.

---

## 🌐 Despliegue en Render (Pregunta 8)

1. En [Render.com](https://render.com/), crea un **New Web Service**.
2. Conecta tu repositorio de GitHub `Gonzalo-rq/Practica-PC2`.
3. Selecciona la rama `deploy/render` (o `main`).
4. Tipo de entorno: **Docker** (Render detectará el `Dockerfile` automáticamente).
5. En la sección **Environment Variables**, añade:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `Redis__ConnectionString` = `superglossy-hobbies-tangerine-38114.db.redis.io:13512,password=T2bE8h7rXhekGlsrqFxHv2CYMMrKf0M7,user=default,ssl=False,abortConnect=false`
   - `RabbitMq__ConnectionString` = `amqps://nrsnmniy:yxEAcsF6CDtVTT9HGmFBi0pR78JRLTFB@jackal.rmq.cloudamqp.com/nrsnmniy`
   - `RabbitMq__QueueName` = `solicitudes.notificaciones`
   - `RabbitMq__ConsumerEnabled` = `true`
   - `ConnectionStrings__DefaultConnection` = `Data Source=/var/data/app.db;Cache=Shared`
6. *(Opcional)*: Agrega un **Persistent Disk** en Render con punto de montaje `/var/data` para conservar la base de datos entre despliegues.
7. Haz clic en **Create Web Service**.

---

## 🌿 Historial de Ramas y Pull Requests en GitHub

El proyecto se estructuró siguiendo la metodología estricta de una rama por pregunta con Pull Request individual hacia `main`:

| Pregunta | Rama | Pull Request | Estado |
| :--- | :--- | :--- | :--- |
| **Pregunta 1** | `feature/bootstrap-dominio` | [PR #1](https://github.com/Gonzalo-rq/Practica-PC2/pull/1) | Merged |
| **Pregunta 2** | `feature/catalogo-solicitudes` | [PR #2](https://github.com/Gonzalo-rq/Practica-PC2/pull/2) | Merged |
| **Pregunta 3** | `feature/solicitudes` | [PR #3](https://github.com/Gonzalo-rq/Practica-PC2/pull/3) | Merged |
| **Pregunta 4** | `feature/sesion-redis` | [PR #4](https://github.com/Gonzalo-rq/Practica-PC2/pull/4) | Merged |
| **Pregunta 5** | `feature/panel-analista` | [PR #5](https://github.com/Gonzalo-rq/Practica-PC2/pull/5) | Merged |
| **Pregunta 6** | `feature/websocket-notificaciones` | [PR #6](https://github.com/Gonzalo-rq/Practica-PC2/pull/6) | Merged |
| **Pregunta 7** | `feature/cloudmq-notificaciones` | [PR #7](https://github.com/Gonzalo-rq/Practica-PC2/pull/7) | Merged |
| **Pregunta 8** | `deploy/render` | [PR #8](https://github.com/Gonzalo-rq/Practica-PC2/pull/8) | Ready / Merged |