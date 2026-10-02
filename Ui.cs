// Custom-drawn UI for FingerprintLockApp: dark theme, animated header,
// gesture cards, segmented picker and an activity timeline.
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using System.ServiceProcess;
using System.Runtime.InteropServices;

namespace FingerprintLock
{
    static class Theme
    {
        public static readonly Color Bg       = Color.FromArgb(13, 15, 28);
        public static readonly Color Surface  = Color.FromArgb(22, 25, 44);
        public static readonly Color Surface2 = Color.FromArgb(31, 35, 60);
        public static readonly Color Border   = Color.FromArgb(46, 51, 84);
        public static readonly Color Text     = Color.FromArgb(238, 240, 255);
        public static readonly Color Muted    = Color.FromArgb(139, 145, 182);
        public static readonly Color Violet   = Color.FromArgb(124, 92, 255);
        public static readonly Color Cyan     = Color.FromArgb(0, 200, 255);
        public static readonly Color Green    = Color.FromArgb(46, 229, 157);
        public static readonly Color Red      = Color.FromArgb(255, 92, 122);
        public static readonly Color Amber    = Color.FromArgb(255, 181, 71);

        public static readonly string IconFamily = PickIconFamily();

        // Segoe Fluent Icons (Windows 11) or Segoe MDL2 Assets (Windows 10).
        public const string GlyphFingerprint = "";
        public const string GlyphSettings = "";
        public const string GlyphUnlock = "";
        public const string GlyphLock = "";
        public const string GlyphWarning = "";
        public const string GlyphPower = "";
        public const string GlyphClock = "";
        public const string GlyphChevron = "";

        static string PickIconFamily()
        {
            foreach (var name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
                using (var f = new Font(name, 10f))
                    if (f.Name == name) return name;
            return "Segoe UI Symbol";
        }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public static Font Ui(float size, FontStyle style = FontStyle.Regular) { return Get("Segoe UI", size, style); }
        public static Font Semibold(float size) { return Get("Segoe UI Semibold", size, FontStyle.Regular); }
        public static Font Icons(float size) { return Get(IconFamily, size, FontStyle.Regular); }
        static Font Get(string family, float size, FontStyle style)
        {
            string key = family + size + style;
            Font f;
            if (!fonts.TryGetValue(key, out f)) { f = new Font(family, size, style, GraphicsUnit.Point); fonts[key] = f; }
            return f;
        }

        public static string Glyph(GestureAction a)
        {
            switch (a)
            {
                case GestureAction.Lock:       return GlyphLock;
                case GestureAction.Sleep:      return "";   // moon
                case GestureAction.ScreenOff:  return "";   // monitor
                case GestureAction.Mute:       return "";
                case GestureAction.PlayPause:  return "";
                case GestureAction.NextTrack:  return "";
                case GestureAction.PrevTrack:  return "";
                case GestureAction.VolumeUp:   return "";
                case GestureAction.VolumeDown: return "";
                default:                       return "";   // cancel
            }
        }

        public static Color WithAlpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static LinearGradientBrush Accent(RectangleF r)
        {
            return new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), Violet, Cyan, 30f);
        }

        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static void DrawCentered(Graphics g, string text, Font font, Color color, RectangleF r)
        {
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(color))
                g.DrawString(text, font, b, r, sf);
        }

        public static Bitmap GlyphBitmap(string glyph, Color color, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                DrawCentered(g, glyph, Icons(size * 0.55f), color, new RectangleF(0, 1, size, size));
            }
            return bmp;
        }

        public static Icon AppIcon(int size)
        {
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Prepare(g);
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var r = new RectangleF(0, 0, size - 1, size - 1);
                    using (var b = Accent(r)) g.FillEllipse(b, r);
                    DrawCentered(g, GlyphFingerprint, Icons(size * 0.42f), Color.White, new RectangleF(0, 1, size, size));
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // Dark context menus (gesture picker and tray menu).
        public static ToolStripRenderer MenuRenderer() { return new DarkMenuRenderer(); }

        class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Text : Muted;
                base.OnRenderItemText(e);
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Muted;
                base.OnRenderArrow(e);
            }
        }

        class DarkColors : ProfessionalColorTable
        {
            static readonly Color Hover = Color.FromArgb(52, 48, 110);
            public override Color ToolStripDropDownBackground { get { return Surface2; } }
            public override Color ImageMarginGradientBegin { get { return Surface2; } }
            public override Color ImageMarginGradientMiddle { get { return Surface2; } }
            public override Color ImageMarginGradientEnd { get { return Surface2; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return Violet; } }
            public override Color MenuItemSelected { get { return Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return Hover; } }
            public override Color MenuItemPressedGradientEnd { get { return Hover; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Surface2; } }
            public override Color CheckBackground { get { return Color.FromArgb(70, 60, 150); } }
            public override Color CheckSelectedBackground { get { return Color.FromArgb(90, 75, 180); } }
            public override Color CheckPressedBackground { get { return Color.FromArgb(90, 75, 180); } }
        }
    }

    abstract class SmoothControl : Control
    {
        protected SmoothControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected static float Approach(float value, float target, float speed)
        {
            float next = value + (target - value) * speed;
            return Math.Abs(target - next) < 0.002f ? target : next;
        }
    }

    // Gradient header with the animated fingerprint, title, status chip and on/off switch.
    class HeaderPanel : SmoothControl
    {
        readonly Timer anim = new Timer { Interval = 30 };
        float pulse, flash, knob;
        bool hoverToggle;

        public bool Listening;
        public bool SwitchOn;
        public string Status = "";
        public Color StatusColor = Theme.Muted;
        public event EventHandler SwitchClicked;

        public HeaderPanel()
        {
            anim.Tick += (s, e) =>
            {
                pulse = (pulse + 0.012f) % 1f;
                flash = Math.Max(0f, flash - 0.03f);
                knob = Approach(knob, SwitchOn ? 1f : 0f, 0.25f);
                Invalidate();
            };
            anim.Start();
        }

        public void Flash() { flash = 1f; }

        RectangleF SwitchRect { get { return new RectangleF(Width - 24 - 64, 70, 64, 34); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = SwitchRect.Contains(e.Location);
            if (over != hoverToggle) { hoverToggle = over; Cursor = over ? Cursors.Hand : Cursors.Default; }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && SwitchRect.Contains(e.Location) && SwitchClicked != null)
                SwitchClicked(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            var full = new RectangleF(0, 0, Width, Height);

            using (var b = new LinearGradientBrush(full, Color.FromArgb(92, 52, 235), Color.FromArgb(0, 150, 210), 15f))
                g.FillRectangle(b, full);
            // Soft decorative blobs for depth.
            using (var b = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
            {
                g.FillEllipse(b, Width - 210, -90, 260, 260);
                g.FillEllipse(b, Width * 0.38f, Height - 70, 160, 160);
            }
            // Fade into the window background.
            var fade = new RectangleF(0, Height - 70, Width, 71);
            using (var b = new LinearGradientBrush(fade, Color.FromArgb(0, Theme.Bg), Theme.Bg, 90f))
                g.FillRectangle(b, fade);

            // Fingerprint with pulse rings while listening, plus a burst when a gesture fires.
            var c = new PointF(88, 88);
            const float R = 44;
            if (Listening)
            {
                for (int i = 0; i < 3; i++)
                {
                    float t = (pulse + i / 3f) % 1f;
                    float r = R + t * 42;
                    using (var p = new Pen(Theme.WithAlpha(Color.White, (int)((1 - t) * 110)), 2f))
                        g.DrawEllipse(p, c.X - r, c.Y - r, r * 2, r * 2);
                }
            }
            if (flash > 0)
            {
                float r = R + (1 - flash) * 70;
                using (var b = new SolidBrush(Theme.WithAlpha(Theme.Cyan, (int)(flash * 140))))
                    g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
            }
            using (var b = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                g.FillEllipse(b, c.X - R, c.Y - R, R * 2, R * 2);
            using (var p = new Pen(Color.FromArgb(150, 255, 255, 255), 2f))
                g.DrawEllipse(p, c.X - R, c.Y - R, R * 2, R * 2);
            Theme.DrawCentered(g, Theme.GlyphFingerprint, Theme.Icons(30f), Color.White, new RectangleF(c.X - R, c.Y - R + 2, R * 2, R * 2));

            // Title and status chip.
            using (var b = new SolidBrush(Color.White))
                g.DrawString("Fingerprint Lock", Theme.Semibold(21f), b, 152, 42);
            var chipFont = Theme.Semibold(9.5f);
            var size = g.MeasureString(Status, chipFont);
            var chip = new RectangleF(156, 90, size.Width + 34, 28);
            using (var path = Theme.Round(chip, 14))
            using (var b = new SolidBrush(Color.FromArgb(120, 10, 12, 30)))
                g.FillPath(b, path);
            float dotAlpha = Listening ? 160 + 95 * (float)Math.Sin(pulse * Math.PI * 2) : 255;
            using (var b = new SolidBrush(Theme.WithAlpha(StatusColor, (int)dotAlpha)))
                g.FillEllipse(b, chip.X + 12, chip.Y + 10, 8, 8);
            using (var b = new SolidBrush(Color.White))
                g.DrawString(Status, chipFont, b, chip.X + 25, chip.Y + 5);

            // On/off switch.
            var sw = SwitchRect;
            using (var path = Theme.Round(sw, sw.Height / 2))
            {
                using (var b = new SolidBrush(Color.FromArgb((int)(60 + knob * 195), 255, 255, 255)))
                    g.FillPath(b, path);
            }
            float kx = sw.X + 4 + knob * (sw.Width - sw.Height);
            var knobRect = new RectangleF(kx, sw.Y + 4, sw.Height - 8, sw.Height - 8);
            if (knob > 0.5f) using (var b = Theme.Accent(knobRect)) g.FillEllipse(b, knobRect);
            else using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, knobRect);
            Theme.DrawCentered(g, SwitchOn ? "Activado" : "Desactivado", Theme.Ui(8.5f), Color.FromArgb(220, 255, 255, 255),
                new RectangleF(sw.X - 20, sw.Bottom + 4, sw.Width + 40, 18));
        }
    }

    // Clickable card showing a gesture and the action assigned to it.
    class GestureCard : SmoothControl
    {
        readonly Timer anim = new Timer { Interval = 30 };
        float hover, glow;
        bool mouseOver;

        public string Title = "", Subtitle = "";
        public int Dots = 1;
        public GestureAction Action;

        public GestureCard()
        {
            Cursor = Cursors.Hand;
            anim.Tick += (s, e) =>
            {
                hover = Approach(hover, mouseOver ? 1f : 0f, 0.25f);
                glow = Math.Max(0f, glow - 0.02f);
                if (hover == (mouseOver ? 1f : 0f) && glow == 0f) anim.Stop();
                Invalidate();
            };
        }

        public void Glow() { glow = 1f; anim.Start(); }
        protected override void OnMouseEnter(EventArgs e) { mouseOver = true; anim.Start(); }
        protected override void OnMouseLeave(EventArgs e) { mouseOver = false; anim.Start(); }
        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Theme.Round(r, 16))
            {
                using (var b = new SolidBrush(Blend(Theme.Surface, Theme.Surface2, hover))) g.FillPath(b, path);
                using (var p = new Pen(Blend(Theme.Border, Theme.Violet, Math.Max(hover * 0.7f, glow)), 1f + glow * 2f)) g.DrawPath(p, path);
            }
            if (glow > 0)
                using (var path = Theme.Round(r, 16))
                using (var b = new SolidBrush(Theme.WithAlpha(Theme.Cyan, (int)(glow * 40))))
                    g.FillPath(b, path);

            // Gesture indicator dots + title.
            for (int i = 0; i < Dots; i++)
            {
                var d = new RectangleF(20 + i * 15, 22, 10, 10);
                using (var b = Theme.Accent(d)) g.FillEllipse(b, d);
            }
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Title, Theme.Semibold(13f), b, 16, 38);
            using (var b = new SolidBrush(Theme.Muted)) g.DrawString(Subtitle, Theme.Ui(9f), b, 18, 64);

            // Assigned action.
            var circle = new RectangleF(20, 100, 46, 46);
            bool none = Action == GestureAction.None;
            if (none) using (var b = new SolidBrush(Theme.Surface2)) g.FillEllipse(b, circle);
            else using (var b = Theme.Accent(circle)) g.FillEllipse(b, circle);
            Theme.DrawCentered(g, Theme.Glyph(Action), Theme.Icons(16f), none ? Theme.Muted : Color.White,
                new RectangleF(circle.X, circle.Y + 1, circle.Width, circle.Height));

            var labelRect = new RectangleF(76, 102, Width - 90, 24);
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var b = new SolidBrush(none ? Theme.Muted : Theme.Text))
                g.DrawString(Actions.Label(Action), Theme.Semibold(11f), b, labelRect, sf);
            using (var b = new SolidBrush(Blend(Theme.Muted, Theme.Cyan, hover)))
                g.DrawString("Cambiar", Theme.Ui(9f), b, 77, 127);
            using (var b = new SolidBrush(Blend(Theme.Muted, Theme.Cyan, hover)))
                g.DrawString(Theme.GlyphChevron, Theme.Icons(7f), b, 132, 132);
        }

        static Color Blend(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // Pill-style segmented picker with an animated selection.
    class Segmented : SmoothControl
    {
        readonly Timer anim = new Timer { Interval = 30 };
        float pos;
        int selected;

        public string[] Options = new string[0];
        public event EventHandler SelectedChanged;

        public Segmented()
        {
            Cursor = Cursors.Hand;
            anim.Tick += (s, e) =>
            {
                pos = Approach(pos, selected, 0.25f);
                if (pos == selected) anim.Stop();
                Invalidate();
            };
        }

        public int Selected
        {
            get { return selected; }
            set { selected = value; anim.Start(); }
        }

        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (!Enabled || Options.Length == 0) return;
            int i = Math.Max(0, Math.Min(Options.Length - 1, e.X * Options.Length / Width));
            if (i == selected) return;
            Selected = i;
            if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Theme.Round(r, r.Height / 2))
            using (var b = new SolidBrush(Theme.Surface))
                g.FillPath(b, path);
            if (Options.Length == 0) return;

            float w = (r.Width - 8) / Options.Length;
            var pill = new RectangleF(r.X + 4 + pos * w, r.Y + 4, w, r.Height - 8);
            using (var path = Theme.Round(pill, pill.Height / 2))
            {
                if (Enabled) using (var b = Theme.Accent(pill)) g.FillPath(b, path);
                else using (var b = new SolidBrush(Theme.Surface2)) g.FillPath(b, path);
            }
            for (int i = 0; i < Options.Length; i++)
            {
                var cell = new RectangleF(r.X + 4 + i * w, r.Y, w, r.Height);
                Color c = !Enabled ? Theme.WithAlpha(Theme.Muted, 120) : i == selected ? Color.White : Theme.Muted;
                Theme.DrawCentered(g, Options[i], i == selected ? Theme.Semibold(10f) : Theme.Ui(10f), c, cell);
            }
        }
    }

    // Rounded dark panel that hosts the activity list.
    class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Prepare(e.Graphics);
            using (var path = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 16))
            {
                using (var b = new SolidBrush(Theme.Surface)) e.Graphics.FillPath(b, path);
                using (var p = new Pen(Theme.Border)) e.Graphics.DrawPath(p, path);
            }
        }
    }

    class ActivityItem
    {
        public string Time, Text, Glyph;
        public Color Color;
        public bool IsGesture, IsDouble;

        // Turns a raw service log line into a friendly timeline entry.
        public static ActivityItem Parse(string line)
        {
            if (line.Length < 21) return null;
            var it = new ActivityItem { Time = line.Substring(11, 8), Color = Theme.Muted, Glyph = Theme.GlyphClock };
            string m = line.Substring(21);

            if (m.StartsWith("gesto: "))
            {
                int arrow = m.IndexOf(" -> ");
                string gesture = arrow > 0 ? m.Substring(7, arrow - 7) : m.Substring(7);
                string label = arrow > 0 ? m.Substring(arrow + 4) : "";
                var action = Actions.All.FirstOrDefault(a => Actions.Label(a) == label);
                it.IsGesture = true;
                it.IsDouble = gesture.StartsWith("doble");
                it.Text = char.ToUpper(gesture[0]) + gesture.Substring(1) + "  →  " + label;
                it.Glyph = Theme.Glyph(action);
                it.Color = action == GestureAction.None ? Theme.Muted : Theme.Violet;
            }
            else if (m.StartsWith("lector reservado")) { it.Text = "Escuchando el lector"; it.Glyph = Theme.GlyphFingerprint; it.Color = Theme.Green; }
            else if (m.StartsWith("lector disponible")) { it.Text = "Lector recuperado"; it.Glyph = Theme.GlyphFingerprint; it.Color = Theme.Green; }
            else if (m.StartsWith("lector liberado")) { it.Text = "Lector liberado"; it.Glyph = Theme.GlyphFingerprint; }
            else if (m.StartsWith("el lector aún está ocupado")) { it.Text = "Lector ocupado, reintentando…"; it.Glyph = Theme.GlyphClock; it.Color = Theme.Amber; }
            else if (m.StartsWith("sesión desbloqueada")) { it.Text = "Sesión desbloqueada"; it.Glyph = Theme.GlyphUnlock; it.Color = Theme.Green; }
            else if (m.StartsWith("sesión no disponible")) { it.Text = "Sesión bloqueada o en pausa"; it.Glyph = Theme.GlyphLock; }
            else if (m.StartsWith("ajustes")) { it.Text = "Ajustes actualizados"; it.Glyph = Theme.GlyphSettings; it.Color = Theme.Cyan; }
            else if (m.StartsWith("toque ignorado")) { it.Text = "Toque ignorado (recién desbloqueado)"; it.Glyph = Theme.GlyphFingerprint; }
            else if (m.StartsWith("servicio iniciado")) { it.Text = "Servicio iniciado"; it.Glyph = Theme.GlyphPower; it.Color = Theme.Cyan; }
            else if (m.StartsWith("servicio detenido")) { it.Text = "Servicio detenido"; it.Glyph = Theme.GlyphPower; }
            else if (m.Contains("falló") || m.Contains("error") || m.StartsWith("no se pudo")) { it.Text = m; it.Glyph = Theme.GlyphWarning; it.Color = Theme.Red; }
            else it.Text = m;
            return it;
        }
    }

    class SettingsForm : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        static readonly int[] WindowValues = { 800, 1200, 1600 };

        readonly HeaderPanel header;
        readonly GestureCard singleCard, doubleCard;
        readonly Segmented window;
        readonly Label hint;
        readonly ListBox activity;
        readonly Timer refresh = new Timer { Interval = 700 };
        readonly ContextMenuStrip picker = new ContextMenuStrip();

        Config config = new Config();
        bool installed;
        string logKey;
        int gestureCount = -1;
        string lastState = "";

        public SettingsForm()
        {
            Text = "Fingerprint Lock";
            Icon = Theme.AppIcon(32);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui(10f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(600, 780);
            DoubleBuffered = true;

            header = new HeaderPanel { Bounds = new Rectangle(0, 0, 600, 190) };
            header.SwitchClicked += (s, e) => { config.Enabled = !config.Enabled; Save(); };
            Controls.Add(header);

            AddSection("GESTOS", 204);
            singleCard = new GestureCard { Bounds = new Rectangle(24, 230, 268, 166), Title = "Un toque", Subtitle = "Toca una vez con tu huella", Dots = 1 };
            doubleCard = new GestureCard { Bounds = new Rectangle(308, 230, 268, 166), Title = "Doble toque", Subtitle = "Dos toques seguidos", Dots = 2 };
            singleCard.Click += (s, e) => OpenPicker(singleCard, true);
            doubleCard.Click += (s, e) => OpenPicker(doubleCard, false);
            Controls.Add(singleCard);
            Controls.Add(doubleCard);

            AddSection("TIEMPO ENTRE LOS DOS TOQUES", 414);
            window = new Segmented { Bounds = new Rectangle(24, 440, 552, 44), Options = new[] { "Rápido · 0,8 s", "Normal · 1,2 s", "Lento · 1,6 s" } };
            window.SelectedChanged += (s, e) => { config.WindowMs = WindowValues[window.Selected]; Save(); };
            Controls.Add(window);

            hint = new Label { Bounds = new Rectangle(26, 492, 550, 40), ForeColor = Theme.Muted, BackColor = Theme.Bg, Font = Theme.Ui(9f) };
            Controls.Add(hint);

            AddSection("ACTIVIDAD", 540);
            var panel = new CardPanel { Bounds = new Rectangle(24, 566, 552, 194), Padding = new Padding(10) };
            activity = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 38, IntegralHeight = false, SelectionMode = SelectionMode.None
            };
            activity.DrawItem += DrawActivity;
            activity.HandleCreated += (s, e) => SetWindowTheme(activity.Handle, "DarkMode_Explorer", null);
            panel.Controls.Add(activity);
            Controls.Add(panel);

            picker.Renderer = Theme.MenuRenderer();
            picker.ImageScalingSize = new Size(22, 22);
            picker.Font = Theme.Ui(10.5f);

            refresh.Tick += (s, e) => RefreshState();
            VisibleChanged += (s, e) =>
            {
                if (Visible) { RefreshState(); refresh.Start(); } else refresh.Stop();
            };
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            DwmSetWindowAttribute(Handle, 20, ref on, 4);             // dark title bar
            int caption = Theme.Bg.R | (Theme.Bg.G << 8) | (Theme.Bg.B << 16);
            DwmSetWindowAttribute(Handle, 35, ref caption, 4);        // caption color (Windows 11)
        }

        void AddSection(string text, int y)
        {
            Controls.Add(new Label
            {
                Text = text, Bounds = new Rectangle(26, y, 400, 20), ForeColor = Theme.Muted,
                BackColor = Theme.Bg, Font = Theme.Semibold(8.5f)
            });
        }

        void OpenPicker(GestureCard card, bool isSingle)
        {
            if (!installed)
            {
                MessageBox.Show(this, "Fingerprint Lock no está instalado. Ejecuta install.ps1 como administrador.", "Fingerprint Lock");
                return;
            }
            picker.Items.Clear();
            foreach (var a in Actions.All)
            {
                var action = a;
                bool current = (isSingle ? config.Single : config.Double) == a;
                var item = new ToolStripMenuItem(Actions.Label(a), Theme.GlyphBitmap(Theme.Glyph(a), current ? Theme.Cyan : Theme.Text, 22))
                {
                    Padding = new Padding(4, 6, 4, 6),
                    Font = current ? Theme.Semibold(10.5f) : Theme.Ui(10.5f)
                };
                item.Click += (s, e) =>
                {
                    if (isSingle) config.Single = action; else config.Double = action;
                    Save();
                };
                picker.Items.Add(item);
                if (a == GestureAction.None || a == GestureAction.ScreenOff) picker.Items.Add(new ToolStripSeparator());
            }
            picker.Show(card, new Point(0, card.Height + 4));
        }

        void Save()
        {
            try { config.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudieron guardar los ajustes:\n" + ex.Message, "Fingerprint Lock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ApplyConfigToUi();
            RefreshState();
        }

        void ApplyConfigToUi()
        {
            header.SwitchOn = config.Enabled;
            singleCard.Action = config.Single;
            doubleCard.Action = config.Double;
            singleCard.Invalidate();
            doubleCard.Invalidate();
            int wi = Array.IndexOf(WindowValues, config.WindowMs);
            if (wi < 0) wi = 1;
            if (window.Selected != wi) window.Selected = wi;
            window.Enabled = installed && config.Double != GestureAction.None;

            bool s = config.Single != GestureAction.None, d = config.Double != GestureAction.None;
            string w = (config.WindowMs / 1000.0).ToString("0.0") + " s";
            hint.ForeColor = Theme.Muted;
            if (!installed) { hint.Text = "Fingerprint Lock no está instalado. Ejecuta install.ps1 como administrador."; hint.ForeColor = Theme.Red; }
            else if (s && d) hint.Text = "Usas los dos gestos: tras un toque se espera " + w + " por si llega el segundo. Solo cuentan huellas registradas.";
            else if (d) hint.Text = "Doble toque = dos toques con tu huella en menos de " + w + ". Solo cuentan huellas registradas.";
            else if (s) hint.Text = "Un toque ejecuta la acción al instante. Solo cuentan huellas registradas.";
            else hint.Text = "No hay acciones asignadas. Toca una tarjeta para elegir qué hace cada gesto.";
        }

        void RefreshState()
        {
            installed = Directory.Exists(Config.Dir);
            try { config = Config.Load(); } catch { }
            ApplyConfigToUi();
            ReadLog();

            string svc;
            try
            {
                using (var sc = new ServiceController("FingerprintLock"))
                    svc = sc.Status == ServiceControllerStatus.Running ? "running" : "stopped";
            }
            catch { svc = "missing"; }

            header.Listening = false;
            if (!installed || svc == "missing") { header.Status = "No instalado"; header.StatusColor = Theme.Red; }
            else if (svc != "running") { header.Status = "Servicio detenido"; header.StatusColor = Theme.Red; }
            else if (!config.Enabled) { header.Status = "Desactivado"; header.StatusColor = Theme.Muted; }
            else if (lastState == "listening") { header.Status = "Escuchando tu huella"; header.StatusColor = Theme.Green; header.Listening = true; }
            else if (lastState == "retrying") { header.Status = "Esperando el lector…"; header.StatusColor = Theme.Amber; }
            else { header.Status = "En espera"; header.StatusColor = Theme.Amber; }
        }

        void ReadLog()
        {
            string text;
            try
            {
                var fi = new FileInfo(Config.LogPath);
                if (!fi.Exists) return;
                string key = fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
                if (key == logKey) return;
                logKey = key;
                using (var fs = new FileStream(Config.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fs))
                    text = reader.ReadToEnd();
            }
            catch { return; }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Current listening state = last state-changing line.
            lastState = "";
            for (int i = lines.Length - 1; i >= 0 && lastState == ""; i--)
            {
                string l = lines[i];
                if (l.Contains("lector reservado") || l.Contains("lector disponible")) lastState = "listening";
                else if (l.Contains("reintentando")) lastState = "retrying";
                else if (l.Contains("lector liberado") || l.Contains("sesión no disponible") || l.Contains("servicio detenido")) lastState = "idle";
            }

            // Flash the header and the matching card when a new gesture appears.
            var gestures = lines.Where(l => l.Contains("  gesto: ")).ToArray();
            if (gestureCount >= 0 && gestures.Length > gestureCount)
            {
                var last = ActivityItem.Parse(gestures[gestures.Length - 1]);
                header.Flash();
                if (last != null) (last.IsDouble ? doubleCard : singleCard).Glow();
            }
            gestureCount = gestures.Length;

            var items = lines.Skip(Math.Max(0, lines.Length - 60)).Reverse()
                             .Select(ActivityItem.Parse).Where(it => it != null).ToArray();
            activity.BeginUpdate();
            activity.Items.Clear();
            activity.Items.AddRange(items);
            activity.EndUpdate();
        }

        void DrawActivity(object sender, DrawItemEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, e.Bounds);
            if (e.Index < 0 || e.Index >= activity.Items.Count) return;
            var it = (ActivityItem)activity.Items[e.Index];
            var r = e.Bounds;

            // Timeline line + icon bubble.
            using (var p = new Pen(Theme.Border, 2f))
                g.DrawLine(p, r.X + 19, r.Y, r.X + 19, r.Bottom);
            var bubble = new RectangleF(r.X + 5, r.Y + 5, 28, 28);
            using (var b = new SolidBrush(Theme.Surface)) g.FillEllipse(b, bubble);
            using (var b = new SolidBrush(Theme.WithAlpha(it.Color, 55))) g.FillEllipse(b, bubble);
            Theme.DrawCentered(g, it.Glyph, Theme.Icons(10f), it.Color, new RectangleF(bubble.X, bubble.Y + 1, bubble.Width, bubble.Height));

            var textRect = new RectangleF(r.X + 44, r.Y, r.Width - 44 - 70, r.Height);
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                using (var b = new SolidBrush(it.IsGesture ? Theme.Text : Theme.WithAlpha(Theme.Text, 210)))
                    g.DrawString(it.Text, it.IsGesture ? Theme.Semibold(10f) : Theme.Ui(9.5f), b, textRect, sf);
                sf.Alignment = StringAlignment.Far;
                using (var b = new SolidBrush(Theme.Muted))
                    g.DrawString(it.Time, Theme.Ui(8.5f), b, new RectangleF(r.Right - 74, r.Y, 66, r.Height), sf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { refresh.Dispose(); picker.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
