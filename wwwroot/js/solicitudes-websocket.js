// solicitudes-websocket.js - Pregunta 6: Notificaciones en tiempo real vía WebSocket con ASP.NET Core SignalR

document.addEventListener("DOMContentLoaded", function () {
    // Si no está disponible signalR en la página, salir
    if (typeof signalR === "undefined") {
        return;
    }

    // Contenedor del estado de la conexión
    const statusContainer = document.createElement("div");
    statusContainer.id = "ws-status-indicator";
    statusContainer.style.position = "fixed";
    statusContainer.style.bottom = "10px";
    statusContainer.style.left = "10px";
    statusContainer.style.zIndex = "1050";
    statusContainer.innerHTML = '<span class="badge bg-secondary" id="ws-badge"><i class="bi bi-broadcast"></i> WebSocket: Conectando...</span>';
    document.body.appendChild(statusContainer);

    function actualizarEstadoConexion(estado, colorClass, texto) {
        const badge = document.getElementById("ws-badge");
        if (badge) {
            badge.className = `badge ${colorClass}`;
            badge.innerHTML = `<i class="bi bi-broadcast"></i> WebSocket: ${texto}`;
        }
    }

    // Configurar conexión forzando transporte WebSocket
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/solicitudes", {
            transport: signalR.HttpTransportType.WebSockets,
            skipNegotiation: false
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Eventos de ciclo de vida de la conexión
    connection.onreconnecting((error) => {
        console.warn("WebSocket reconectando...", error);
        actualizarEstadoConexion("reconnecting", "bg-warning text-dark", "Reconectando...");
    });

    connection.onreconnected((connectionId) => {
        console.log("WebSocket reconectado con ConnectionId:", connectionId);
        actualizarEstadoConexion("connected", "bg-success", "Conectado");
        recuperarEstadoActualAlReconectar();
    });

    connection.onclose((error) => {
        console.error("WebSocket desconectado:", error);
        actualizarEstadoConexion("disconnected", "bg-danger", "Desconectado");
    });

    // Escuchar el evento SolicitudEstadoActualizado emitido por el servidor
    connection.on("SolicitudEstadoActualizado", function (data) {
        console.log("Evento recibido SolicitudEstadoActualizado:", data);
        mostrarAvisoEnPantalla(data);
        actualizarVistas(data);
    });

    // Iniciar conexión
    connection.start()
        .then(() => {
            console.log("Conectado exitosamente al Hub de Solicitudes vía WebSocket.");
            actualizarEstadoConexion("connected", "bg-success", "Conectado");
        })
        .catch((err) => {
            console.error("Error al conectar al Hub WebSocket:", err);
            actualizarEstadoConexion("error", "bg-danger", "Error de conexión");
        });

    // Función para mostrar notificación flotante / toast
    function mostrarAvisoEnPantalla(data) {
        const toastContainer = document.getElementById("toast-container");
        if (!toastContainer) return;

        const toastId = "toast-" + Date.now();
        const color = data.estado === "Aprobado" ? "success" : "danger";
        const icono = data.estado === "Aprobado" ? "check-circle-fill" : "x-circle-fill";

        let htmlMotivo = "";
        if (data.motivoRechazo) {
            htmlMotivo = `<div class="mt-1 small"><strong>Motivo:</strong> ${data.motivoRechazo}</div>`;
        }

        const toastHtml = `
            <div id="${toastId}" class="toast align-items-center text-white bg-${color} border-0 shadow-lg" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="bi bi-${icono} me-2 fs-5"></i>
                        <strong>¡Solicitud #${data.solicitudId} ${data.estado}!</strong>
                        ${htmlMotivo}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;
        toastContainer.insertAdjacentHTML("beforeend", toastHtml);
        const toastElement = document.getElementById(toastId);
        if (typeof bootstrap !== "undefined" && bootstrap.Toast) {
            const bsToast = new bootstrap.Toast(toastElement, { delay: 10000 });
            bsToast.show();
        }
    }

    // Función para actualizar las vistas ("Mis solicitudes" y "Detalle")
    function actualizarVistas(data) {
        // 1. Actualizar fila en la tabla de "Mis solicitudes" si existe
        const fila = document.getElementById(`solicitud-row-${data.solicitudId}`);
        if (fila) {
            const badge = fila.querySelector(".solicitud-badge");
            if (badge) {
                badge.className = `badge solicitud-badge ${data.estado === "Aprobado" ? "bg-success" : data.estado === "Rechazado" ? "bg-danger" : "bg-warning text-dark"}`;
                badge.textContent = data.estado;
            }
            const motivoCell = fila.querySelector(".solicitud-motivo");
            if (motivoCell) {
                motivoCell.textContent = data.motivoRechazo || "-";
            }
            // Efecto de parpadeo suave
            fila.classList.add("table-info");
            setTimeout(() => fila.classList.remove("table-info"), 2500);
        }

        // 2. Actualizar tarjeta de la vista Detalle si coincide el ID
        const detalleBadge = document.getElementById("detalle-badge");
        const detalleTexto = document.getElementById("detalle-estado-texto");
        const urlPartes = window.location.pathname.split("/");
        const idActual = parseInt(urlPartes[urlPartes.length - 1]);

        if (detalleBadge && idActual === data.solicitudId) {
            detalleBadge.className = `badge ${data.estado === "Aprobado" ? "bg-success" : data.estado === "Rechazado" ? "bg-danger" : "bg-warning text-dark"} fs-6`;
            detalleBadge.textContent = data.estado;

            if (detalleTexto) {
                detalleTexto.textContent = data.estado;
            }

            const motivoContenedor = document.getElementById("detalle-motivo-container");
            const motivoTexto = document.getElementById("detalle-motivo-texto");
            if (motivoContenedor && motivoTexto) {
                if (data.motivoRechazo) {
                    motivoTexto.textContent = data.motivoRechazo;
                    motivoContenedor.classList.remove("d-none");
                } else {
                    motivoContenedor.classList.add("d-none");
                }
            }
        }
    }

    // Función de recuperación al reconectar
    function recuperarEstadoActualAlReconectar() {
        const urlPartes = window.location.pathname.split("/");
        const idActual = parseInt(urlPartes[urlPartes.length - 1]);
        if (!isNaN(idActual) && window.location.pathname.toLowerCase().includes("/solicitudes/detalle")) {
            fetch(`/Solicitudes/EstadoActual/${idActual}`)
                .then(res => res.ok ? res.json() : null)
                .then(data => {
                    if (data) {
                        console.log("Estado sincronizado tras reconexión:", data);
                        actualizarVistas(data);
                    }
                })
                .catch(err => console.warn("Error sincronizando estado tras reconexión:", err));
        }
    }
});
