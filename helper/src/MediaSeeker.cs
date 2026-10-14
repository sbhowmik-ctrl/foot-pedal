using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace PedalKeys
{
    /// <summary>
    /// Seeks the playing video or audio by a whole second. YouTube's left/right arrows skip
    /// 5 seconds, and many local-file players ignore arrows entirely, so this talks to the
    /// Windows media session of the focused app instead of sending those keys.
    /// </summary>
    internal static class MediaSeeker
    {
        const string ManagerTypeName =
            "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime";
        const string AsyncInfoTypeName =
            "Windows.Foundation.IAsyncInfo, Windows.Foundation, ContentType=WindowsRuntime";
        const string AsyncOpTypeName =
            "Windows.Foundation.IAsyncOperation`1, Windows.Foundation, ContentType=WindowsRuntime";

        static readonly object gate = new object();
        static object manager;
        static bool managerFailed;
        static int pendingDelta;
        static bool flushing;
        static string lastAumid;
        static TimeSpan lastTarget;
        static bool hasTarget;
        static DateTime lastSeekAt;
        // Steps closer together than this continue from the last target (hold repeats every
        // 200 ms; the first repeat comes 400 ms after the press).
        const int ChainWindowMs = 900;
        // How long we trust our own position over a player that has not updated its timeline.
        const int StaleTimelineSeconds = 60;
        static AutomationElement seekSlider;
        static IntPtr seekSliderHwnd;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        public static void Warmup()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { GetManager(); }
                catch { }
            });
        }

        /// <summary>
        /// Queue a 1-second step. Never blocks the pedal reader: a slow Windows media
        /// call was dropping the release report, so a second tap and hold-to-repeat never fired.
        /// </summary>
        public static void SeekBySeconds(int deltaSeconds)
        {
            if (deltaSeconds == 0) return;
            bool startFlush;
            lock (gate)
            {
                pendingDelta += deltaSeconds;
                startFlush = !flushing;
                if (startFlush) flushing = true;
            }
            if (startFlush)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Flush(); }
                    catch
                    {
                        lock (gate) { flushing = false; pendingDelta = 0; }
                    }
                });
            }
        }

        static void Flush()
        {
            while (true)
            {
                int delta;
                lock (gate)
                {
                    delta = pendingDelta;
                    pendingDelta = 0;
                    if (delta == 0)
                    {
                        flushing = false;
                        return;
                    }
                }

                bool ok = false;
                try { ok = TrySeekUiSlider(delta); }
                catch { }
                if (!ok)
                {
                    try { ok = TrySeekSession(delta); }
                    catch { }
                }
                if (!ok)
                {
                    try { ok = TrySeekUiSlider(delta); }
                    catch { }
                }
                // Only send YouTube's 5s arrows when a real 1s seek was not possible.
                if (!ok) KeySender.Tap(delta < 0 ? KeySender.VK_LEFT : KeySender.VK_RIGHT);
            }
        }

        static bool TrySeekSession(int deltaSeconds)
        {
            object mgr = GetManager();
            if (mgr == null) return false;

            string processName = ForegroundProcessName();
            string windowTitle = ForegroundWindowTitle();

            object matched = null;
            object sessions = Call(mgr, "GetSessions");
            bool uwpHost = IsUwpHost(processName);
            if (sessions != null)
            {
                foreach (object session in (System.Collections.IEnumerable)sessions)
                {
                    if (!CanSeek(session)) continue;
                    string aumid = Convert.ToString(Prop(session, "SourceAppUserModelId"));
                    string mediaTitle = uwpHost ? MediaTitle(session) : "";
                    if (MatchesForeground(aumid, processName, windowTitle, mediaTitle))
                    {
                        matched = session;
                        break;
                    }
                }
            }
            if (matched == null) return false;
            return SeekSession(matched, deltaSeconds);
        }

        static bool SeekSession(object session, int deltaSeconds)
        {
            object timeline = Call(session, "GetTimelineProperties");
            if (timeline == null) return false;

            string aumid = Convert.ToString(Prop(session, "SourceAppUserModelId")) ?? "";
            TimeSpan reported = (TimeSpan)Prop(timeline, "Position");
            TimeSpan live = EstimateLivePosition(session, timeline, reported);
            TimeSpan end = (TimeSpan)Prop(timeline, "EndTime");
            TimeSpan min = (TimeSpan)Prop(timeline, "MinSeekTime");
            TimeSpan max = (TimeSpan)Prop(timeline, "MaxSeekTime");

            TimeSpan step = TimeSpan.FromSeconds(deltaSeconds);

            // While tapping or holding, keep stepping from our own last target. The player's
            // reported time lags and is rounded, so re-reading it every step made the time
            // bounce between two neighbouring seconds. Re-read only after a pause.
            TimeSpan basis = live;
            bool chained = false;
            DateTimeOffset playerUpdated = LastUpdated(timeline);
            bool playing = IsPlaying(session);
            double rate = PlaybackRate(session);
            if (rate <= 0) rate = 1;
            lock (gate)
            {
                if (hasTarget && aumid == lastAumid)
                {
                    TimeSpan age = DateTime.UtcNow - lastSeekAt;
                    if (age.TotalMilliseconds < ChainWindowMs)
                    {
                        basis = lastTarget;
                        chained = true;
                    }
                    else if (age.TotalSeconds < StaleTimelineSeconds
                        && playerUpdated <= new DateTimeOffset(lastSeekAt, TimeSpan.Zero) + TimeSpan.FromMilliseconds(500))
                    {
                        // The player has not published a new position since our last seek, so
                        // its reported time is from before the seek (e.g. before a long hold
                        // on back). Continue from where we put it, plus time played since.
                        basis = lastTarget + (playing ? TimeSpan.FromSeconds(age.TotalSeconds * rate) : TimeSpan.Zero);
                        chained = true;
                    }
                }
            }

            TimeSpan next = basis + step;
            // A 1s pedal step must not follow a stale timeline glitch (first step only).
            if (!chained && Math.Abs((next - reported).TotalSeconds) > 2.5)
                next = reported + step;
            if (next < TimeSpan.Zero) next = TimeSpan.Zero;
            if (end > TimeSpan.Zero && next > end) next = end;
            if (max > TimeSpan.Zero && next > max) next = max;
            if (min > TimeSpan.Zero && next < min
                && !(deltaSeconds < 0 && (live - min).TotalSeconds < 2))
                next = min;

            MethodInfo seek = session.GetType().GetMethod("TryChangePlaybackPositionAsync");
            if (seek == null) return false;
            object op = seek.Invoke(session, new object[] { next.Ticks });
            object ok = WaitOp(op, typeof(bool));
            bool success = ok is bool && (bool)ok;
            if (!success) return false;
            lock (gate)
            {
                lastTarget = next;
                lastAumid = aumid;
                hasTarget = true;
                lastSeekAt = DateTime.UtcNow;
            }
            return true;
        }

        static bool TrySeekUiSlider(int deltaSeconds)
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            AutomationElement slider = GetSeekSlider(hwnd);
            if (slider == null) return false;

            RangeValuePattern range = slider.GetCurrentPattern(RangeValuePattern.Pattern) as RangeValuePattern;
            if (range == null) return false;

            double cur = range.Current.Value;
            double min = range.Current.Minimum;
            double max = range.Current.Maximum;
            // YouTube's seek bar is seconds. A 0–100 percent bar is not a 1s step.
            if (max <= min || max <= 100) return false;

            // The slider value is rounded to whole seconds and updates late, so chain from
            // the last value we set while tapping or holding instead of re-reading it.
            string key = "ui:" + hwnd.ToInt64();
            double basis = cur;
            lock (gate)
            {
                if (hasTarget && key == lastAumid
                    && (DateTime.UtcNow - lastSeekAt).TotalMilliseconds < ChainWindowMs)
                    basis = lastTarget.TotalSeconds;
            }

            double next = basis + deltaSeconds;
            if (next < min) next = min;
            if (next > max) next = max;
            if (Math.Abs(next - basis) < 0.05 && Math.Abs(next - cur) < 0.05) return false;
            range.SetValue(next);
            lock (gate)
            {
                lastTarget = TimeSpan.FromSeconds(next);
                lastAumid = key;
                hasTarget = true;
                lastSeekAt = DateTime.UtcNow;
            }
            return true;
        }

        static AutomationElement GetSeekSlider(IntPtr hwnd)
        {
            lock (gate)
            {
                if (seekSlider != null && seekSliderHwnd == hwnd)
                {
                    try
                    {
                        if (seekSlider.Current.ProcessId != 0) return seekSlider;
                    }
                    catch
                    {
                        seekSlider = null;
                    }
                }
            }

            AutomationElement found = null;
            Thread finder = new Thread(delegate()
            {
                try
                {
                    AutomationElement root = AutomationElement.FromHandle(hwnd);
                    if (root == null) return;
                    found = root.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.NameProperty, "Seek slider"));
                }
                catch { }
            });
            finder.IsBackground = true;
            finder.SetApartmentState(ApartmentState.STA);
            finder.Start();
            if (!finder.Join(250)) return null;
            if (found == null) return null;
            lock (gate)
            {
                seekSlider = found;
                seekSliderHwnd = hwnd;
            }
            return found;
        }

        static bool IsPlaying(object session)
        {
            try
            {
                object info = Call(session, "GetPlaybackInfo");
                if (info == null) return false;
                object status = Prop(info, "PlaybackStatus");
                return Convert.ToInt32(status) == 3
                    || string.Equals(Convert.ToString(status), "Playing", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        static double PlaybackRate(object session)
        {
            try
            {
                object info = Call(session, "GetPlaybackInfo");
                if (info == null) return 1.0;
                object rate = Prop(info, "PlaybackRate");
                if (rate == null) return 1.0;
                if (rate is double) return (double)rate;
                PropertyInfo value = rate.GetType().GetProperty("Value");
                if (value != null) return Convert.ToDouble(value.GetValue(rate, null));
                return Convert.ToDouble(rate);
            }
            catch
            {
                return 1.0;
            }
        }

        /// <summary>When the player last published its position, or MinValue if unknown.</summary>
        static DateTimeOffset LastUpdated(object timeline)
        {
            try
            {
                object lu = Prop(timeline, "LastUpdatedTime");
                if (lu is DateTimeOffset) return (DateTimeOffset)lu;
                if (lu is DateTime) return new DateTimeOffset((DateTime)lu);
            }
            catch { }
            return DateTimeOffset.MinValue;
        }

        static TimeSpan EstimateLivePosition(object session, object timeline, TimeSpan reported)
        {
            if (!IsPlaying(session)) return reported;
            try
            {
                object lu = Prop(timeline, "LastUpdatedTime");
                DateTimeOffset updated;
                if (lu is DateTimeOffset) updated = (DateTimeOffset)lu;
                else if (lu is DateTime) updated = new DateTimeOffset((DateTime)lu);
                else return reported;

                TimeSpan lag = DateTimeOffset.Now - updated;
                double rate = PlaybackRate(session);
                if (rate <= 0) rate = 1;
                double extra = lag.TotalSeconds * rate;
                // Chrome often leaves LastUpdatedTime ~10s stale. Adding all of that
                // made the first skip jump 10s, then later skips were 1s.
                if (extra > 0 && extra < 15)
                {
                    if (extra > 1) extra = 1;
                    reported = reported + TimeSpan.FromSeconds(extra);
                }
                return reported;
            }
            catch
            {
                return reported;
            }
        }

        static bool CanSeek(object session)
        {
            object info = Call(session, "GetPlaybackInfo");
            if (info == null) return false;
            object controls = Prop(info, "Controls");
            if (controls == null) return false;
            object enabled = Prop(controls, "IsPlaybackPositionEnabled");
            return enabled is bool && (bool)enabled;
        }

        static string MediaTitle(object session)
        {
            try
            {
                MethodInfo m = session.GetType().GetMethod("TryGetMediaPropertiesAsync");
                if (m == null) return "";
                object op = m.Invoke(session, null);
                Type propsType = Type.GetType(
                    "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType=WindowsRuntime");
                object props = WaitOp(op, propsType);
                if (props == null) return "";
                return Convert.ToString(Prop(props, "Title")) ?? "";
            }
            catch
            {
                return "";
            }
        }

        static bool MatchesForeground(string aumid, string processName, string windowTitle, string mediaTitle)
        {
            if (string.IsNullOrEmpty(aumid)) return false;
            string id = aumid.ToLowerInvariant();
            string proc = (processName ?? "").ToLowerInvariant();

            if (proc.Length > 0 && id.IndexOf(proc) >= 0) return true;
            if (proc == "chrome" && id.IndexOf("chrome") >= 0) return true;
            if (proc == "msedge" && (id.IndexOf("msedge") >= 0 || id.IndexOf("edge") >= 0)) return true;
            if (proc == "brave" && id.IndexOf("brave") >= 0) return true;
            if (proc == "opera" && id.IndexOf("opera") >= 0) return true;
            if (proc == "firefox" && id.IndexOf("firefox") >= 0) return true;
            if (proc == "vlc" && id.IndexOf("vlc") >= 0) return true;
            if (proc == "spotify" && id.IndexOf("spotify") >= 0) return true;
            if (proc == "wmplayer" && id.IndexOf("media") >= 0) return true;
            if (proc.IndexOf("media.player") >= 0 && id.IndexOf("media.player") >= 0) return true;
            if (proc == "music.ui" && id.IndexOf("zunemusic") >= 0) return true;
            if (proc == "video.ui" && id.IndexOf("zunevideo") >= 0) return true;
            if (proc == "photos" && id.IndexOf("photos") >= 0) return true;

            bool uwpHost = IsUwpHost(proc);
            if (uwpHost)
            {
                if (!string.IsNullOrEmpty(mediaTitle) && !string.IsNullOrEmpty(windowTitle)
                    && windowTitle.IndexOf(mediaTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (LooksLikeMediaWindow(windowTitle)
                    && (id.IndexOf("zunemusic") >= 0 || id.IndexOf("zunevideo") >= 0
                        || id.IndexOf("media.player") >= 0 || id.IndexOf("zune") >= 0
                        || id.IndexOf("photos") >= 0))
                    return true;
            }
            return false;
        }

        static bool IsUwpHost(string processName)
        {
            string proc = (processName ?? "").ToLowerInvariant();
            return proc == "applicationframehost" || proc == "shellexperiencehost";
        }

        static bool LooksLikeMediaWindow(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle)) return false;
            string t = windowTitle.ToLowerInvariant();
            return t.IndexOf("media player") >= 0 || t.IndexOf("groove") >= 0
                || t.IndexOf("movies") >= 0 || t.IndexOf("photos") >= 0
                || t.IndexOf("music") >= 0 || t.IndexOf("film") >= 0;
        }

        static object GetManager()
        {
            lock (gate)
            {
                if (manager != null) return manager;
                if (managerFailed) return null;
            }
            try
            {
                Type mgrType = Type.GetType(ManagerTypeName);
                if (mgrType == null)
                {
                    lock (gate) { managerFailed = true; }
                    return null;
                }
                object op = mgrType.GetMethod("RequestAsync").Invoke(null, null);
                object loaded = WaitOp(op, mgrType);
                lock (gate)
                {
                    if (manager == null) manager = loaded;
                    if (manager == null) managerFailed = true;
                    return manager;
                }
            }
            catch
            {
                lock (gate) { managerFailed = true; }
                return null;
            }
        }

        static object WaitOp(object asyncOp, Type resultType)
        {
            if (asyncOp == null || resultType == null) return null;
            Type infoType = Type.GetType(AsyncInfoTypeName);
            Type opGeneric = Type.GetType(AsyncOpTypeName);
            if (infoType == null || opGeneric == null) return null;
            PropertyInfo statusProp = infoType.GetProperty("Status");
            MethodInfo getResults = opGeneric.MakeGenericType(resultType).GetMethod("GetResults");
            int status = 0;
            for (int i = 0; i < 40; i++)
            {
                status = Convert.ToInt32(statusProp.GetValue(asyncOp, null));
                if (status != 0) break;
                Thread.Sleep(10);
            }
            if (status != 1) return null;
            return getResults.Invoke(asyncOp, null);
        }

        static object Prop(object o, string name)
        {
            return o.GetType().GetProperty(name).GetValue(o, null);
        }

        static object Call(object o, string name)
        {
            MethodInfo m = o.GetType().GetMethod(name);
            return m == null ? null : m.Invoke(o, null);
        }

        static string ForegroundProcessName()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "";
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return "";
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                return "";
            }
        }

        static string ForegroundWindowTitle()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "";
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(hwnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }
    }
}
