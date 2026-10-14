using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PedalKeys
{
    /// <summary>The whole UI: a tray icon and a context menu. No windows, no mapping screen.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Control ui = new Control();
        readonly ToolStripMenuItem status = new ToolStripMenuItem();
        readonly ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause mappings");
        readonly ToolStripMenuItem resumeItem = new ToolStripMenuItem("Resume mappings");
        readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        readonly ToolStripMenuItem quitItem = new ToolStripMenuItem("Quit");
        readonly PedalMonitor monitor;
        volatile bool paused;
        bool announced;

        public TrayApp(string logPath)
        {
            ui.CreateControl();
            IntPtr force = ui.Handle; // make sure BeginInvoke has a message loop to post to

            status.Enabled = false;
            pauseItem.Click += delegate { SetPaused(true); };
            resumeItem.Click += delegate { SetPaused(false); };
            startupItem.Click += delegate { ToggleStartup(); };
            quitItem.Click += delegate { Quit(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(pauseItem);
            menu.Items.Add(resumeItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(quitItem);

            tray.ContextMenuStrip = menu;
            tray.Visible = true;

            Startup.ApplyDefaultOnFirstRun();
            startupItem.Checked = Startup.IsEnabled;

            RefreshUi();

            // Mappings are live from here on; there is nothing to configure.
            monitor = new PedalMonitor(delegate { return paused; }, logPath);
            monitor.ConnectionChanged += delegate { Post(OnConnectionChanged); };
        }

        void Post(Action a)
        {
            try { if (!ui.IsDisposed) ui.BeginInvoke(a); } catch { }
        }

        void OnConnectionChanged()
        {
            RefreshUi();
            if (announced) return;
            announced = true;
            if (monitor.Connected)
                Balloon("Pedal connected. Left = back, Center = play/pause, Right = forward.");
            else
                Balloon("Pedal not found. Plug it in; I'll keep looking.");
        }

        void SetPaused(bool value)
        {
            paused = value;
            RefreshUi();
        }

        void ToggleStartup()
        {
            try { Startup.SetEnabled(!Startup.IsEnabled); }
            catch (Exception e) { Balloon("Couldn't change startup setting: " + e.Message); }
            startupItem.Checked = Startup.IsEnabled;
        }

        void RefreshUi()
        {
            bool connected = monitor != null && monitor.Connected;
            string text;
            Color color;
            if (paused) { text = "Paused"; color = Color.FromArgb(150, 150, 150); }
            else if (connected) { text = "Pedal connected"; color = Color.FromArgb(34, 160, 90); }
            else { text = "Pedal not found. Retrying..."; color = Color.FromArgb(230, 150, 20); }

            status.Text = text;
            pauseItem.Visible = !paused;
            resumeItem.Visible = paused;
            tray.Text = "PedalKeys: " + text; // NotifyIcon limits this to 63 chars

            Icon old = tray.Icon;
            tray.Icon = MakeIcon(color, paused);
            if (old != null) { DestroyIcon(old.Handle); old.Dispose(); }
        }

        void Balloon(string text)
        {
            tray.BalloonTipTitle = "PedalKeys";
            tray.BalloonTipText = text;
            tray.ShowBalloonTip(4000);
        }

        void Quit()
        {
            tray.Visible = false;
            monitor.Dispose();
            tray.Dispose();
            Application.Exit();
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        // Draws a small pedal glyph in the status colour: green = connected,
        // amber = pedal not found, grey = paused (with pause bars).
        static Icon MakeIcon(Color color, bool pausedGlyph)
        {
            int size = SystemInformation.SmallIconSize.Width;
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Brush fill = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float u = size / 16f;
                using (GraphicsPath p = RoundRect(1 * u, 3 * u, 14 * u, 10 * u, 3 * u))
                    g.FillPath(fill, p);
                if (pausedGlyph)
                {
                    g.FillRectangle(Brushes.White, 5.5f * u, 5.5f * u, 2 * u, 5 * u);
                    g.FillRectangle(Brushes.White, 9 * u, 5.5f * u, 2 * u, 5 * u);
                }
                else
                {
                    g.FillEllipse(Brushes.White, 4 * u, 6 * u, 3 * u, 3 * u);
                    g.FillEllipse(Brushes.White, 9 * u, 6 * u, 3 * u, 3 * u);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, 2 * r, 2 * r, 180, 90);
            p.AddArc(x + w - 2 * r, y, 2 * r, 2 * r, 270, 90);
            p.AddArc(x + w - 2 * r, y + h - 2 * r, 2 * r, 2 * r, 0, 90);
            p.AddArc(x, y + h - 2 * r, 2 * r, 2 * r, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
