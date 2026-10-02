# Registro de cambios

## v1.0.0 — 2026-10-02

Primera versión pública.

- Servicio de Windows que escucha el lector de huellas mientras la sesión está desbloqueada.
- Gestos de **un toque** y **doble toque**, cada uno con su propia acción.
- Acciones:
  - bloquear el PC, suspender y apagar la pantalla;
  - reproducir/pausar, siguiente pista y pista anterior;
  - subir volumen, bajar volumen y silenciar.
- Tiempo entre toques configurable (0,8 / 1,2 / 1,6 s).
- Solo cuentan huellas registradas. Se ignoran los toques durante los 3 s siguientes a desbloquear.
- Suelta el lector al bloquear, suspender o cerrar sesión, para que Windows Hello siga funcionando al iniciar sesión.
- Reintenta reservar el lector si al desbloquear aún lo tiene la pantalla de inicio.
- App de bandeja con tema oscuro:
  - estado en vivo con animación;
  - tarjetas por gesto;
  - línea de tiempo de actividad.
- Instalador y desinstalador en PowerShell, con compilación automática si faltan los ejecutables.
