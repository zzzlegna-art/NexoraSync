# NEXORA SYNC 0.2.1

Aplicación de escritorio Windows para construir un espacio privado de archivos compartidos entre equipos autorizados.

## Estado de esta versión

Esta versión incorpora la **primera base del motor local de cambios**:
- Monitor de cambios de archivos y carpetas en la ubicación seleccionada.
- Detección de creación, modificación, eliminación y renombrado de archivos y carpetas.
- Consolidación de operaciones pendientes por ruta para evitar duplicados y cancelar creaciones eliminadas antes de sincronizar.
- Cola local persistente, separada por carpeta y conservada entre reinicios.
- El contador representa entradas pendientes consolidadas, no el número bruto de eventos del sistema.

## Importante: sincronización remota todavía en desarrollo

Esta versión **todavía no transfiere archivos entre computadores**. No incluye servidor remoto operativo, autenticación de dispositivos, transferencia, sincronización bidireccional, resolución de conflictos ni respaldo remoto. La cola local es una base de trabajo para esas siguientes etapas; no debe considerarse una copia de seguridad.

## Compilar

El workflow de GitHub Actions compila la aplicación WPF para Windows x64 y genera el instalador Inno Setup. El nombre del artefacto se obtiene de la versión declarada en el proyecto.
