// FingerprintLock service: while the console session is unlocked, holds the
// fingerprint sensor and turns recognized touches into gestures (single or
// double tap), running the action configured for each one.
using System;
using System.IO;
using System.Threading;
using System.ServiceProcess;
using System.Runtime.InteropServices;

namespace FingerprintLock
{
    static class Native
    {
        // --- winbio.dll
        public const uint WINBIO_TYPE_FINGERPRINT = 0x08;
        public const uint WINBIO_POOL_SYSTEM = 1;
        public const uint WINBIO_FLAG_DEFAULT = 0;
        public static readonly IntPtr WINBIO_DB_DEFAULT = new IntPtr(1);
        public const uint WINBIO_E_UNKNOWN_ID = 0x80098003;
        public const uint WINBIO_E_CANCELED = 0x80098004;
        public const uint WINBIO_E_NO_MATCH = 0x80098005;
        public const uint WINBIO_E_BAD_CAPTURE = 0x80098008;

        [DllImport("winbio.dll")] public static extern int WinBioOpenSession(uint factor, uint pool, uint flags, IntPtr units, UIntPtr unitCount, IntPtr dbId, out uint session);
        [DllImport("winbio.dll")] public static extern int WinBioIdentify(uint session, out uint unitId, IntPtr identity, out byte subFactor, out uint rejectDetail);
        [DllImport("winbio.dll")] public static extern int WinBioCancel(uint session);
        [DllImport("winbio.dll")] public static extern int WinBioCloseSession(uint session);
        [DllImport("winbio.dll")] public static extern int WinBioAcquireFocus();
        [DllImport("winbio.dll")] public static extern int WinBioReleaseFocus();

        // --- sessions
        public const uint NO_SESSION = 0xFFFFFFFF;
        const int WTSSessionInfoEx = 25;
        const int WTSActive = 0;
        const int WTS_SESSIONSTATE_UNLOCK = 1;

        [DllImport("kernel32.dll")] public static extern uint WTSGetActiveConsoleSessionId();
        [DllImport("wtsapi32.dll", SetLastError = true)] static extern bool WTSQuerySessionInformationW(IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out uint bytes);
        [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(IntPtr p);
        [DllImport("wtsapi32.dll", SetLastError = true)] static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);
        [DllImport("wtsapi32.dll", SetLastError = true)] public static extern bool WTSDisconnectSession(IntPtr server, uint sessionId, bool wait);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        // True when the console session has a logged-on user and is unlocked.
        public static bool IsConsoleSessionUnlocked()
        {
            uint sid = WTSGetActiveConsoleSessionId();
            if (sid == NO_SESSION) return false;
            IntPtr buf; uint bytes;
            if (!WTSQuerySessionInformationW(IntPtr.Zero, sid, WTSSessionInfoEx, out buf, out bytes)) return false;
            try
            {
                // WTSINFOEXW { DWORD Level; union { WTSINFOEX_LEVEL1_W { ULONG SessionId; WTS_CONNECTSTATE_CLASS State; LONG SessionFlags; ... } } }
                // The union holds LARGE_INTEGER fields, so it is 8-byte aligned: SessionId is at offset 8, not 4.
                int level = Marshal.ReadInt32(buf, 0);
                int state = Marshal.ReadInt32(buf, 12);
                int flags = Marshal.ReadInt32(buf, 16);
                return level == 1 && state == WTSActive && flags == WTS_SESSIONSTATE_UNLOCK;
            }
            finally { WTSFreeMemory(buf); }
        }

        // --- process creation in the user's session
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
        const uint CREATE_NO_WINDOW = 0x08000000;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcessAsUserW(IntPtr token, string app, string cmd, IntPtr pa, IntPtr ta, bool inherit,
            uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

        // Starts exe as the user of the console session. Returns null on success, else an error description.
        public static string RunInConsoleSession(string exe, string args)
        {
            uint sid = WTSGetActiveConsoleSessionId();
            if (sid == NO_SESSION) return "no hay sesión activa";
            IntPtr token;
            if (!WTSQueryUserToken(sid, out token)) return "WTSQueryUserToken error " + Marshal.GetLastWin32Error();
            try
            {
                var si = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFO)), lpDesktop = @"winsta0\default" };
                PROCESS_INFORMATION pi;
                if (!CreateProcessAsUserW(token, exe, "\"" + exe + "\" " + args, IntPtr.Zero, IntPtr.Zero, false,
                        CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
                    return "CreateProcessAsUser error " + Marshal.GetLastWin32Error();
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
                return null;
            }
            finally { CloseHandle(token); }
        }
    }

    class FingerprintLockService : ServiceBase
    {
        // Ignore touches right after unlocking, in case the finger used to sign in is still on the sensor.
        static readonly TimeSpan UnlockGrace = TimeSpan.FromSeconds(3);
        static readonly string AppExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FingerprintLockApp.exe");

        readonly object gate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        Config config = new Config();
        bool sessionUnlocked;
        bool stopping;
        uint bioSession;                // open WinBio session while listening, else 0
        DateTime unlockedAt = DateTime.MinValue;
        DateTime? pendingTouch;         // first touch of a possible double tap
        int pendingId;
        Timer pendingTimer;
        int focusFailures;              // consecutive WinBioAcquireFocus failures (worker thread only)
        Thread worker;
        FileSystemWatcher watcher;

        public FingerprintLockService()
        {
            ServiceName = "FingerprintLock";
            CanHandleSessionChangeEvent = true;
            CanHandlePowerEvent = true;
            CanStop = true;
        }

        // Must be called under gate.
        bool ShouldListen { get { return sessionUnlocked && config.Enabled && !stopping; } }

        static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Config.Dir);
                if (File.Exists(Config.LogPath) && new FileInfo(Config.LogPath).Length > 1024 * 1024)
                {
                    File.Delete(Config.LogPath + ".old");
                    File.Move(Config.LogPath, Config.LogPath + ".old");
                }
                File.AppendAllText(Config.LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        static string Hr(int hr) { return "0x" + hr.ToString("X8"); }

        protected override void OnStart(string[] args)
        {
            Log("servicio iniciado");
            Directory.CreateDirectory(Config.Dir);
            LoadConfig("inicio");
            SetSession(Native.IsConsoleSessionUnlocked(), "inicio");

            watcher = new FileSystemWatcher(Config.Dir, "config.ini");
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
            watcher.Changed += (s, e) => LoadConfig("cambio");
            watcher.Created += (s, e) => LoadConfig("cambio");
            watcher.Renamed += (s, e) => LoadConfig("cambio");
            watcher.EnableRaisingEvents = true;

            worker = new Thread(Work) { IsBackground = true, Name = "fingerprint" };
            worker.Start();
        }

        protected override void OnStop()
        {
            lock (gate) { stopping = true; UpdateListening(); }
            if (watcher != null) watcher.Dispose();
            if (worker != null) worker.Join(5000);
            Log("servicio detenido");
        }

        protected override void OnSessionChange(SessionChangeDescription d)
        {
            string why = d.Reason + " sesión " + d.SessionId;
            switch (d.Reason)
            {
                case SessionChangeReason.SessionUnlock:
                case SessionChangeReason.SessionLogon:
                    if ((uint)d.SessionId == Native.WTSGetActiveConsoleSessionId()) SetSession(true, why);
                    break;
                case SessionChangeReason.SessionLock:
                case SessionChangeReason.SessionLogoff:
                case SessionChangeReason.ConsoleDisconnect:
                    SetSession(false, why);
                    break;
                case SessionChangeReason.ConsoleConnect:
                    SetSession(Native.IsConsoleSessionUnlocked(), why);
                    break;
            }
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus status)
        {
            if (status == PowerBroadcastStatus.Suspend)
                SetSession(false, "suspensión");
            else if (status == PowerBroadcastStatus.ResumeSuspend || status == PowerBroadcastStatus.ResumeAutomatic)
                SetSession(Native.IsConsoleSessionUnlocked(), "reanudación");
            return true;
        }

        void LoadConfig(string why)
        {
            Config c;
            try { c = Config.Load(); }
            catch (Exception ex) { Log("no se pudo leer config.ini (" + ex.Message + "), se mantienen los ajustes"); return; }
            lock (gate)
            {
                if (c.Describe() == config.Describe() && why != "inicio") return;
                config = c;
                pendingTouch = null;
                Log("ajustes (" + why + "): " + c.Describe());
                UpdateListening();
            }
        }

        void SetSession(bool unlocked, string why)
        {
            lock (gate)
            {
                if (unlocked && !sessionUnlocked) unlockedAt = DateTime.Now;
                if (unlocked != sessionUnlocked) Log((unlocked ? "sesión desbloqueada" : "sesión no disponible") + " (" + why + ")");
                sessionUnlocked = unlocked;
                if (!unlocked) pendingTouch = null;
                UpdateListening();
            }
        }

        // Must be called under gate. Canceling makes the blocked WinBioIdentify return, which releases the sensor.
        void UpdateListening()
        {
            if (!ShouldListen && bioSession != 0) Native.WinBioCancel(bioSession);
            wake.Set();
        }

        void Work()
        {
            while (true)
            {
                bool listen;
                lock (gate) { if (stopping) return; listen = ShouldListen; }
                if (!listen) { focusFailures = 0; wake.WaitOne(); continue; }

                try { Listen(); }
                catch (Exception ex) { Log("error: " + ex); wake.WaitOne(5000); }
            }
        }

        void Listen()
        {
            uint s;
            int hr = Native.WinBioOpenSession(Native.WINBIO_TYPE_FINGERPRINT, Native.WINBIO_POOL_SYSTEM, Native.WINBIO_FLAG_DEFAULT,
                IntPtr.Zero, UIntPtr.Zero, Native.WINBIO_DB_DEFAULT, out s);
            if (hr != 0) { Log("WinBioOpenSession falló " + Hr(hr)); wake.WaitOne(5000); return; }

            lock (gate)
            {
                if (!ShouldListen) { Native.WinBioCloseSession(s); return; }
                bioSession = s;
            }

            // Right after unlocking, the sign-in screen may still own the sensor and AcquireFocus is
            // denied. Without focus no touches arrive, so back off and retry until it succeeds.
            hr = Native.WinBioAcquireFocus();
            if (hr != 0)
            {
                if (focusFailures == 0) Log("el lector aún está ocupado (" + Hr(hr) + "), reintentando...");
                focusFailures++;
                lock (gate) bioSession = 0;
                Native.WinBioCloseSession(s);
                wake.WaitOne(focusFailures < 10 ? 500 : 2000);
                return;
            }
            if (focusFailures > 0) Log("lector disponible tras " + focusFailures + " reintentos");
            focusFailures = 0;

            IntPtr identity = Marshal.AllocHGlobal(128);
            bool failed = false;
            try
            {
                Log("lector reservado, escuchando");

                while (true)
                {
                    lock (gate) { if (!ShouldListen) break; }
                    uint unit; byte sub; uint reject;
                    hr = Native.WinBioIdentify(s, out unit, identity, out sub, out reject);
                    uint u = (uint)hr;
                    if (u == Native.WINBIO_E_CANCELED)
                    {
                        // Either we canceled on purpose (checked at the top of the loop) or Windows timed the read out.
                        Thread.Sleep(100);
                        continue;
                    }
                    if (hr == 0) { OnRecognizedTouch(); continue; }
                    if (u == Native.WINBIO_E_UNKNOWN_ID || u == Native.WINBIO_E_NO_MATCH || u == Native.WINBIO_E_BAD_CAPTURE)
                        continue;   // accidental or unrecognized touch
                    Log("WinBioIdentify falló " + Hr(hr) + ", reabriendo el lector");
                    failed = true;
                    break;
                }
            }
            finally
            {
                lock (gate) bioSession = 0;
                Native.WinBioReleaseFocus();
                Native.WinBioCloseSession(s);
                Marshal.FreeHGlobal(identity);
                Log("lector liberado");
            }
            if (failed) wake.WaitOne(2000);
        }

        void OnRecognizedTouch()
        {
            DateTime now = DateTime.Now;
            lock (gate)
            {
                Config c = config;
                if (now - unlockedAt < UnlockGrace) { Log("toque ignorado (recién desbloqueado)"); return; }

                if (c.Double == GestureAction.None) { Fire("un toque", c.Single); return; }

                if (pendingTouch.HasValue && (now - pendingTouch.Value).TotalMilliseconds <= c.WindowMs)
                {
                    pendingTouch = null;
                    pendingId++;
                    Fire("doble toque", c.Double);
                    return;
                }

                // First touch: wait to see whether a second one follows.
                pendingTouch = now;
                int id = ++pendingId;
                GestureAction single = c.Single;
                if (pendingTimer != null) pendingTimer.Dispose();
                pendingTimer = new Timer(_ =>
                {
                    lock (gate)
                    {
                        if (pendingId != id || !pendingTouch.HasValue) return;
                        pendingTouch = null;
                    }
                    Fire("un toque", single);
                }, null, c.WindowMs, Timeout.Infinite);
            }
        }

        void Fire(string gesture, GestureAction action)
        {
            Log("gesto: " + gesture + " -> " + Actions.Label(action));
            if (action == GestureAction.None) return;
            ThreadPool.QueueUserWorkItem(_ => Execute(action));
        }

        void Execute(GestureAction action)
        {
            bool leavesSession = action == GestureAction.Lock || action == GestureAction.Sleep;
            // Release the sensor before the sign-in screen needs it.
            if (leavesSession) SetSession(false, "acción " + Actions.Key(action));

            string err = Native.RunInConsoleSession(AppExe, "--action " + Actions.Key(action));
            if (err != null)
            {
                Log("no se pudo ejecutar la acción: " + err);
                if (action == GestureAction.Lock)
                {
                    // Fallback: disconnecting the console session also shows the sign-in screen.
                    uint sid = Native.WTSGetActiveConsoleSessionId();
                    if (Native.WTSDisconnectSession(IntPtr.Zero, sid, false)) Log("sesión desconectada (alternativa al bloqueo)");
                }
            }

            if (leavesSession)
            {
                // If the session is still unlocked after a while, the action did not happen: listen again.
                Thread.Sleep(5000);
                if (Native.IsConsoleSessionUnlocked()) SetSession(true, "la sesión sigue desbloqueada");
            }
        }

        static void Main()
        {
            ServiceBase.Run(new FingerprintLockService());
        }
    }
}
