// FingerprintLockApp: tray icon + settings window. Also runs gesture actions
// inside the user's session when the service launches it with --action.
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using System.ServiceProcess;
using System.Runtime.InteropServices;

namespace FingerprintLock
{
    static class ActionRunner
    {
        [DllImport("user32.dll")] static extern bool LockWorkStation();
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("powrprof.dll")] static extern bool SetSuspendState(bool hibernate, bool force, bool wakeupEventsDisabled);
        [DllImport("advapi32.dll")] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern bool LookupPrivilegeValueW(string system, string name, out long luid);
        [DllImport("advapi32.dll")] static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivilege state, int len, IntPtr prev, IntPtr retLen);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct TokenPrivilege { public int Count; public long Luid; public int Attributes; }

        // Media keys are handled system-wide, so pressing them works whatever window is active.
        const byte VK_VOLUME_MUTE = 0xAD, VK_VOLUME_DOWN = 0xAE, VK_VOLUME_UP = 0xAF;
        const byte VK_MEDIA_NEXT_TRACK = 0xB0, VK_MEDIA_PREV_TRACK = 0xB1, VK_MEDIA_PLAY_PAUSE = 0xB3;
        const uint KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2;

        static int PressKey(byte vk)
        {
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            return 0;
        }
        const uint WM_SYSCOMMAND = 0x0112;
        const int SC_MONITORPOWER = 0xF170;
        const uint SMTO_ABORTIFHUNG = 2;

        public static int Run(string key)
        {
            switch (Actions.Parse(key, GestureAction.None))
            {
                case GestureAction.Lock:
                    return LockWorkStation() ? 0 : 1;
                case GestureAction.ScreenOff:
                    IntPtr r;
                    SendMessageTimeout(new IntPtr(0xFFFF), WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(2), SMTO_ABORTIFHUNG, 1000, out r);
                    return 0;
                case GestureAction.Mute:       return PressKey(VK_VOLUME_MUTE);
                case GestureAction.VolumeUp:   return PressKey(VK_VOLUME_UP);
                case GestureAction.VolumeDown: return PressKey(VK_VOLUME_DOWN);
                case GestureAction.PlayPause:  return PressKey(VK_MEDIA_PLAY_PAUSE);
                case GestureAction.NextTrack:  return PressKey(VK_MEDIA_NEXT_TRACK);
                case GestureAction.PrevTrack:  return PressKey(VK_MEDIA_PREV_TRACK);
                case GestureAction.Sleep:
                    EnableShutdownPrivilege();
                    return SetSuspendState(false, false, false) ? 0 : 1;
            }
            return 2;
        }

        static void EnableShutdownPrivilege()
        {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), 0x0020 /* ADJUST_PRIVILEGES */ | 0x0008 /* QUERY */, out token)) return;
            try
            {
                var tp = new TokenPrivilege { Count = 1, Attributes = 2 /* SE_PRIVILEGE_ENABLED */ };
                if (LookupPrivilegeValueW(null, "SeShutdownPrivilege", out tp.Luid))
                    AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { CloseHandle(token); }
        }
    }

    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon icon;
        readonly ToolStripMenuItem enabledItem;
        readonly Control invoker = new Control();
        SettingsForm form;

        public TrayApp(EventWaitHandle showSignal, bool openSettings)
        {
            var h = invoker.Handle;     // create the handle so BeginInvoke works from other threads

            var menu = new ContextMenuStrip { Renderer = Theme.MenuRenderer(), Font = Theme.Ui(10f) };
            menu.Items.Add("Abrir ajustes", null, (s, e) => ShowSettings());
            enabledItem = new ToolStripMenuItem("Activado", null, (s, e) => ToggleEnabled());
            menu.Items.Add(enabledItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, (s, e) => { icon.Visible = false; ExitThread(); });
            menu.Opening += (s, e) =>
            {
                try { enabledItem.Checked = Config.Load().Enabled; } catch { }
            };

            icon = new NotifyIcon { Icon = Theme.AppIcon(32), Text = "Fingerprint Lock", ContextMenuStrip = menu, Visible = true };
            icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };

            // A second launch of the app signals this instance to show the settings.
            new Thread(() =>
            {
                while (true)
                {
                    showSignal.WaitOne();
                    invoker.BeginInvoke((Action)ShowSettings);
                }
            }) { IsBackground = true }.Start();

            if (openSettings) invoker.BeginInvoke((Action)ShowSettings);
        }

        void ShowSettings()
        {
            if (form == null) form = new SettingsForm();
            form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
        }

        void ToggleEnabled()
        {
            try
            {
                var c = Config.Load();
                c.Enabled = !c.Enabled;
                c.Save();
                icon.ShowBalloonTip(2000, "Fingerprint Lock", c.Enabled ? "Activado" : "Desactivado", ToolTipIcon.Info);
                if (form != null && form.Visible) { form.Hide(); form.Show(); }   // reload the controls
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudieron guardar los ajustes:\n" + ex.Message, "Fingerprint Lock");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { icon.Dispose(); invoker.Dispose(); if (form != null) form.Dispose(); }
            base.Dispose(disposing);
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--action")
                return ActionRunner.Run(args[1]);

            bool created;
            using (var mutex = new Mutex(true, @"Local\FingerprintLockApp", out created))
            using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\FingerprintLockApp.Show"))
            {
                if (!created) { show.Set(); return 0; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool openSettings = !(args.Length == 1 && args[0] == "--tray");
                Application.Run(new TrayApp(show, openSettings));
            }
            return 0;
        }
    }
}
