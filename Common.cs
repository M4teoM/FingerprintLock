// Shared by the service and the app: gesture actions and the settings file.
using System;
using System.IO;
using System.Threading;

namespace FingerprintLock
{
    public enum GestureAction { None, Lock, Sleep, ScreenOff, Mute, PlayPause, NextTrack, PrevTrack, VolumeUp, VolumeDown }

    public static class Actions
    {
        public static readonly GestureAction[] All =
        {
            GestureAction.None, GestureAction.Lock, GestureAction.Sleep, GestureAction.ScreenOff,
            GestureAction.PlayPause, GestureAction.NextTrack, GestureAction.PrevTrack,
            GestureAction.VolumeUp, GestureAction.VolumeDown, GestureAction.Mute
        };

        public static string Label(GestureAction a)
        {
            switch (a)
            {
                case GestureAction.Lock:      return "Bloquear el PC";
                case GestureAction.Sleep:     return "Suspender";
                case GestureAction.ScreenOff: return "Apagar la pantalla";
                case GestureAction.Mute:      return "Silenciar / activar sonido";
                case GestureAction.PlayPause: return "Reproducir / pausar";
                case GestureAction.NextTrack: return "Siguiente pista";
                case GestureAction.PrevTrack: return "Pista anterior";
                case GestureAction.VolumeUp:  return "Subir volumen";
                case GestureAction.VolumeDown: return "Bajar volumen";
                default:                      return "Nada";
            }
        }

        public static string Key(GestureAction a) { return a.ToString().ToLowerInvariant(); }

        public static GestureAction Parse(string s, GestureAction fallback)
        {
            foreach (var a in All)
                if (Key(a) == s) return a;
            return fallback;
        }
    }

    public class Config
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FingerprintLock");
        public static readonly string ConfigPath = Path.Combine(Dir, "config.ini");
        public static readonly string LogPath = Path.Combine(Dir, "service.log");

        public bool Enabled = true;
        public GestureAction Single = GestureAction.None;
        public GestureAction Double = GestureAction.Lock;
        public int WindowMs = 1200;     // max time between the two touches of a double tap

        // Throws if the file is unreadable or incomplete (e.g. caught mid-write), so callers keep the previous settings.
        public static Config Load()
        {
            var c = new Config();
            if (!File.Exists(ConfigPath)) return c;

            string[] lines;
            for (int attempt = 0; ; attempt++)
            {
                try { lines = File.ReadAllLines(ConfigPath); break; }
                catch (IOException) { if (attempt >= 4) throw; Thread.Sleep(50); }
            }

            int seen = 0;
            foreach (var raw in lines)
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                string k = raw.Substring(0, eq).Trim().ToLowerInvariant();
                string v = raw.Substring(eq + 1).Trim().ToLowerInvariant();
                if (k == "enabled") { c.Enabled = v == "1" || v == "true"; seen++; }
                else if (k == "single") { c.Single = Actions.Parse(v, c.Single); seen++; }
                else if (k == "double") { c.Double = Actions.Parse(v, c.Double); seen++; }
                else if (k == "window")
                {
                    int ms;
                    if (int.TryParse(v, out ms)) c.WindowMs = Math.Max(300, Math.Min(3000, ms));
                    seen++;
                }
            }
            if (seen < 4) throw new InvalidDataException("config.ini incompleto");
            return c;
        }

        public void Save()
        {
            File.WriteAllText(ConfigPath,
                "enabled=" + (Enabled ? "1" : "0") + "\r\n" +
                "single=" + Actions.Key(Single) + "\r\n" +
                "double=" + Actions.Key(Double) + "\r\n" +
                "window=" + WindowMs + "\r\n");
        }

        public string Describe()
        {
            return string.Format("activado={0}, un toque={1}, doble toque={2}, ventana={3} ms",
                Enabled ? "sí" : "no", Actions.Label(Single), Actions.Label(Double), WindowMs);
        }
    }
}
