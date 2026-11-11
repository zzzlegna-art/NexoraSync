# NEXORA SYNC 0.1.2

Aplicación de escritorio Windows para validar la experiencia visual y el flujo inicial de configuración.

## Estado de esta versión

Esta entrega es un **prototipo de interfaz instalable**, no una versión operativa de sincronización en nube. Incluye:
- Aplicación WPF para Windows x64.
- Selector de tema oscuro/claro (cambio visual básico).
- Selección de carpeta local.
- Guardado local de preferencias.
- Pipeline de GitHub Actions para compilar un instalador Inno Setup.

Aún no incluye sincronización real, servidor remoto, autenticación, transferencia de archivos, resolución de conflictos ni servicio en segundo plano. No usar con datos importantes ni como respaldo.

## Obtener el instalador

1. Abre la pestaña **Actions** del repositorio.
2. Selecciona **Build NEXORA SYNC Windows Installer**.
3. Pulsa **Run workflow** si no se ha iniciado automáticamente.
4. Espera a que termine correctamente.
5. Entra al run completado y descarga el artefacto `NexoraSync-Setup-0.1.2`.

El instalador se compila en un runner Windows de GitHub Actions.
