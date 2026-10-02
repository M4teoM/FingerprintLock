# Fingerprint Lock

Convierte el lector de huellas de tu portátil con Windows en un botón con gestos. Con la sesión desbloqueada, al tocar el lector con una huella registrada se ejecuta la acción que elijas:

| Gesto | Ejemplo |
|---|---|
| **Un toque** | Reproducir / pausar |
| **Doble toque** | Bloquear el PC |

Acciones disponibles: bloquear el PC, suspender, apagar la pantalla, reproducir/pausar, siguiente pista, pista anterior, subir volumen, bajar volumen y silenciar/activar sonido.

![Ventana de ajustes](docs/screenshot.png)

## Cómo funciona

- **`FingerprintLock.exe`** es un servicio de Windows que corre como `LocalSystem`. Windows solo entrega los toques del lector a la ventana en primer plano, así que el servicio reserva el lector con `WinBioAcquireFocus` para recibirlos en segundo plano.
- **Solo escucha mientras la sesión está desbloqueada.** Al bloquear, suspender o cerrar sesión suelta el lector, de modo que Windows Hello sigue funcionando para iniciar sesión. Si al desbloquear el lector aún está ocupado por la pantalla de inicio, reintenta hasta reservarlo.
- **Solo cuentan huellas registradas** en Windows Hello. Los toques accidentales o de dedos no registrados se ignoran.
- **Las acciones se ejecutan dentro de tu sesión.** Para eso el servicio lanza `FingerprintLockApp.exe --action ...` con `CreateProcessAsUser`.
- **`FingerprintLockApp.exe`** es la app de la bandeja: muestra la ventana de ajustes, el estado en vivo y la actividad. Los ajustes se guardan en `C:\ProgramData\FingerprintLock\config.ini` y el servicio los aplica al momento.

## Instalación

Requisitos: Windows 10/11 con lector de huellas configurado en Windows Hello. No hace falta instalar nada más: se compila con el compilador de C# que trae Windows (.NET Framework 4).

En PowerShell **como administrador**:

```powershell
git clone https://github.com/M4teoM/FingerprintLock.git
powershell -NoProfile -ExecutionPolicy Bypass -File .\FingerprintLock\install.ps1
```

Luego abre **Fingerprint Lock** desde el menú Inicio. Para desinstalar, ejecuta `uninstall.ps1` como administrador.

## Limitaciones

- **No hay toque mantenido.** El lector captura una vez al apoyar el dedo y no avisa cuándo lo levantas, así que un toque largo es indistinguible de uno corto.
- **La huella deja de servir para otras cosas con la sesión abierta.** Mientras el servicio escucha, el lector está reservado: los avisos de Windows Hello (UAC, passkeys, gestores de contraseñas) no lo recibirán. En esos casos usa el PIN o el reconocimiento facial, o desactiva la función desde el interruptor de la app.
- **El toque simple puede tardar un poco.** Si asignas acciones a los dos gestos, espera el tiempo configurado por si llega un segundo toque.

## Archivos

| Archivo | Contenido |
|---|---|
| `Service.cs` | Servicio: lector, sesiones, gestos y lanzamiento de acciones |
| `App.cs` | App de bandeja y ejecución de acciones (`--action`) |
| `Ui.cs` | Interfaz (tema oscuro, tarjetas, línea de tiempo) |
| `Common.cs` | Acciones y archivo de ajustes compartidos |
| `build.ps1` / `install.ps1` / `uninstall.ps1` | Compilar, instalar y desinstalar |

## Licencia

MIT
