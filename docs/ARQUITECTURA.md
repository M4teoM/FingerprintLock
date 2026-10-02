# Arquitectura

Fingerprint Lock tiene dos ejecutables que se comunican mediante archivos en `C:\ProgramData\FingerprintLock`:

```
┌──────────────────────────────┐        config.ini         ┌─────────────────────────────┐
│  FingerprintLock.exe         │ ◄──────────────────────── │  FingerprintLockApp.exe     │
│  servicio, LocalSystem,      │                           │  bandeja + ajustes          │
│  sesión 0                    │ ────────────────────────► │  (sesión del usuario)       │
│                              │        service.log        │                             │
│  lector ─► gestos ─► acción  │                           │                             │
│                 │            │   CreateProcessAsUser     │                             │
│                 └────────────┼─────────────────────────► │  FingerprintLockApp.exe     │
│                              │                           │  --action <acción>          │
└──────────────────────────────┘                           └─────────────────────────────┘
```

## Por qué un servicio

El Windows Biometric Framework (WinBio) solo entrega las capturas del *pool* del sistema al proceso que tiene la ventana en primer plano. Una app normal en segundo plano no recibe ningún toque.

Un servicio sin ventana puede pedir el lector explícitamente con `WinBioAcquireFocus`, y así recibir las capturas aunque el usuario esté en otra aplicación. Fingerprint Lock la hace desde un servicio que corre como `LocalSystem`.

## Ciclo de vida del lector

El servicio solo tiene el lector mientras se cumplen **las tres** condiciones:

1. la sesión de consola está desbloqueada;
2. la función está activada en `config.ini`;
3. el servicio no se está deteniendo.

Las transiciones llegan por `OnSessionChange` (bloquear, desbloquear, iniciar o cerrar sesión, conectar o desconectar la consola) y `OnPowerEvent` (suspender, reanudar).

- **Cuando deja de cumplirse alguna condición**, se llama a `WinBioCancel`. Eso hace volver a la `WinBioIdentify` que está esperando, y el bucle libera el lector con `WinBioReleaseFocus` y `WinBioCloseSession`. Así, la pantalla de inicio de sesión puede usar la huella con normalidad.
- **Justo al desbloquear**, la pantalla de inicio puede seguir teniendo el lector, y `WinBioAcquireFocus` devuelve `E_ACCESSDENIED` (`0x80070005`). El servicio cierra la sesión del lector y reintenta: cada 0,5 s los 10 primeros intentos y después cada 2 s.
- **Windows a veces cancela por su cuenta** una `WinBioIdentify` que lleva tiempo esperando (`WINBIO_E_CANCELED`). Si las condiciones siguen cumpliéndose, el servicio simplemente vuelve a llamarla.

### Estado de la sesión

Para saber si la sesión de consola está desbloqueada se usa `WTSQuerySessionInformation(WTSSessionInfoEx)`. La estructura `WTSINFOEXW` contiene una unión con campos `LARGE_INTEGER`, así que va alineada a 8 bytes:

| Offset | Campo |
|---|---|
| 0 | `Level` (1) |
| 8 | `SessionId` |
| 12 | `SessionState` (0 = `WTSActive`) |
| 16 | `SessionFlags` (1 = `WTS_SESSIONSTATE_UNLOCK`) |

## Gestos

`WinBioIdentify` devuelve un resultado por cada vez que se apoya el dedo:

- `S_OK`: huella registrada reconocida. Es el único resultado que cuenta como toque.
- `WINBIO_E_UNKNOWN_ID`, `WINBIO_E_NO_MATCH`, `WINBIO_E_BAD_CAPTURE`: dedo no registrado o captura mala. Se ignoran.

Con el reconocimiento como entrada:

- **Sin acción de doble toque**, cada toque ejecuta la acción del toque simple al instante.
- **Con acción de doble toque**, el primer toque queda pendiente durante la ventana configurada:
  - si llega otro dentro de la ventana, se ejecuta el **doble toque**;
  - si no llega, al vencer el temporizador se ejecuta el **toque simple** (si tiene acción).
- Se ignoran los toques durante los 3 s siguientes a desbloquear, por si el dedo con el que se entró sigue apoyado.

No hay "toque mantenido": el lector captura una sola vez al apoyar el dedo y no informa de cuándo se levanta.

## Ejecución de acciones

`LockWorkStation`, las teclas multimedia y `SC_MONITORPOWER` tienen que ejecutarse **dentro de la sesión del usuario**, no en la sesión 0 del servicio. Por eso el servicio:

1. obtiene el token del usuario con `WTSQueryUserToken`;
2. lanza `FingerprintLockApp.exe --action <acción>` con `CreateProcessAsUser` en `winsta0\default`.

Esa instancia ejecuta la acción y termina:

| Acción | Implementación |
|---|---|
| Bloquear | `LockWorkStation()` |
| Suspender | activa `SeShutdownPrivilege` y llama a `SetSuspendState(false, false, false)` |
| Apagar pantalla | `SendMessageTimeout(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2)` |
| Multimedia y volumen | `keybd_event` con `VK_MEDIA_*` / `VK_VOLUME_*` |

**Bloquear y suspender** sacan al usuario de la sesión, así que el servicio suelta el lector **antes** de ejecutarlas. Si 5 s después la sesión sigue desbloqueada (la acción no llegó a ocurrir), vuelve a escuchar. Si lanzar el proceso falla al bloquear, como alternativa desconecta la sesión con `WTSDisconnectSession`, que también lleva a la pantalla de inicio.

## Ajustes y registro

**`config.ini`:**

```ini
enabled=1
single=none
double=lock
window=1200
```

- Valores de acción: `none`, `lock`, `sleep`, `screenoff`, `mute`, `playpause`, `nexttrack`, `prevtrack`, `volumeup`, `volumedown`.
- El instalador da permiso de modificación a `BUILTIN\Users`, para que la app pueda guardarlo sin privilegios.
- El servicio lo vigila con un `FileSystemWatcher` y aplica los cambios al momento. Si lo lee a medio escribir (faltan claves), lo descarta y mantiene los ajustes anteriores.

**`service.log`:**
- Rota al pasar de 1 MB y guarda la copia anterior como `service.log.old`.
- La app lo lee cada 0,7 s, solo si cambió su tamaño o su fecha. Con él deduce el estado (escuchando, reintentando, en pausa), dibuja la línea de tiempo y anima la cabecera cuando aparece un gesto nuevo.

## Compilación

`build.ps1` usa `csc.exe` de .NET Framework 4 (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`), con `/codepage:65001` para que los acentos del código se lean bien:

| Ejecutable | Fuentes | Referencias |
|---|---|---|
| `FingerprintLock.exe` | `Service.cs` + `Common.cs` | `System.ServiceProcess` |
| `FingerprintLockApp.exe` | `App.cs` + `Ui.cs` + `Common.cs` | `System.Windows.Forms`, `System.Drawing`, `System.ServiceProcess` |
