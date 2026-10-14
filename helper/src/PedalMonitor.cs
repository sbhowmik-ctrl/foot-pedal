using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PedalKeys
{
    /// <summary>
    /// Looks for the foot pedal (VEC Infinity style, 05F3:00FF) and keeps looking while the
    /// app is open. Handles plug, unplug and replug without a restart.
    /// </summary>
    internal sealed class PedalMonitor : IDisposable
    {
        public const ushort VendorId = 0x05F3;
        public const ushort ProductId = 0x00FF;
        const int RetryMs = 2000;

        readonly Func<bool> isPaused;
        readonly object gate = new object();
        readonly HashSet<string> active = new HashSet<string>();
        readonly Timer scanTimer;
        readonly string logPath;
        bool disposed;
        bool lastReported;

        /// <summary>Raised (on a worker thread) when the pedal appears or disappears.</summary>
        public event Action<bool> ConnectionChanged;

        public PedalMonitor(Func<bool> isPaused, string logPath)
        {
            this.isPaused = isPaused;
            this.logPath = logPath;
            MediaSeeker.Warmup();
            scanTimer = new Timer(delegate { Scan(); }, null, 0, RetryMs);
        }

        public bool Connected
        {
            get { lock (gate) return active.Count > 0; }
        }

        void Scan()
        {
            try
            {
                lock (gate) { if (disposed) return; }

                foreach (string path in Hid.FindPaths(VendorId, ProductId))
                {
                    lock (gate) { if (disposed || active.Contains(path)) continue; }

                    Hid.Reader reader = Hid.Reader.Open(path);
                    if (reader == null) continue; // e.g. keyboard-class collection Windows won't share

                    lock (gate) { active.Add(path); }
                    Thread t = new Thread(delegate() { Pump(path, reader); });
                    t.IsBackground = true;
                    t.Name = "PedalKeys reader";
                    t.Start();
                }
            }
            catch { /* never let a scan failure take the tray app down; we retry anyway */ }

            Publish();
        }

        void Pump(string path, Hid.Reader reader)
        {
            Mapper mapper = new Mapper(isPaused);
            Publish();
            try
            {
                using (reader)
                {
                    byte[] data;
                    while ((data = reader.Read()) != null)
                    {
                        Log(data);
                        if (data.Length > 0) mapper.Update(data[0]);
                    }
                }
            }
            catch { }
            finally
            {
                mapper.Dispose();
                lock (gate) { active.Remove(path); }
                Publish();
            }
        }

        void Publish()
        {
            bool now = Connected;
            Action<bool> handler;
            lock (gate)
            {
                if (now == lastReported && !firstPublish) return;
                firstPublish = false;
                lastReported = now;
                handler = ConnectionChanged;
            }
            if (handler != null) handler(now);
        }
        bool firstPublish = true;

        // Opt-in diagnostics: run `PedalKeys.exe --log` to record raw reports.
        void Log(byte[] data)
        {
            if (logPath == null) return;
            try
            {
                File.AppendAllText(logPath,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + BitConverter.ToString(data) + Environment.NewLine);
            }
            catch { }
        }

        public void Dispose()
        {
            lock (gate) { disposed = true; }
            scanTimer.Dispose();
        }
    }
}
