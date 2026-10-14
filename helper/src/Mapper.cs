using System;
using System.Threading;

namespace PedalKeys
{
    /// <summary>
    /// Turns pedal button state into playback commands.
    ///   Left   -> seek back 1 second,  fires on press, then repeats every 200 ms while held
    ///   Center -> Space,               fires once per press, never repeats
    ///   Right  -> seek forward 1 second, fires on press, then repeats every 200 ms while held
    /// </summary>
    internal sealed class Mapper : IDisposable
    {
        public const int RepeatDelayMs = 400;
        public const int RepeatMs = 200;

        // Bit positions in the first data byte of the pedal's input report.
        const byte LEFT = 0x01;
        const byte CENTER = 0x02;
        const byte RIGHT = 0x04;

        readonly Func<bool> isPaused;
        readonly Action<ushort> send;
        readonly Action<int> seekBySeconds;
        readonly object gate = new object();
        Timer leftTimer, rightTimer;
        byte previous;
        bool disposed;

        public Mapper(Func<bool> isPaused) : this(isPaused, KeySender.Tap, MediaSeeker.SeekBySeconds) { }

        // The send callback is injectable so the logic can be tested without a real pedal.
        public Mapper(Func<bool> isPaused, Action<ushort> send) : this(isPaused, send, null) { }

        public Mapper(Func<bool> isPaused, Action<ushort> send, Action<int> seekBySeconds)
        {
            this.isPaused = isPaused;
            this.send = send;
            this.seekBySeconds = seekBySeconds;
        }

        /// <summary>Feed the current button bitmask. Call on every input report.</summary>
        public void Update(byte buttons)
        {
            buttons &= (byte)(LEFT | CENTER | RIGHT);
            bool tapSpace = false;
            int seekDelta = 0;
            ushort seekFallback = 0;
            lock (gate)
            {
                if (disposed) return;

                if (isPaused())
                {
                    // Keep tracking so a pedal held across "Resume" doesn't fire a stale press.
                    StopTimers();
                    previous = buttons;
                    return;
                }

                byte pressed = (byte)(buttons & ~previous);
                byte released = (byte)(previous & ~buttons);
                previous = buttons;

                if ((pressed & CENTER) != 0) tapSpace = true;
                if ((pressed & LEFT) != 0)
                {
                    Begin(ref leftTimer, LEFT, -1, KeySender.VK_LEFT);
                    seekDelta = -1;
                    seekFallback = KeySender.VK_LEFT;
                }
                if ((pressed & RIGHT) != 0)
                {
                    Begin(ref rightTimer, RIGHT, 1, KeySender.VK_RIGHT);
                    seekDelta = 1;
                    seekFallback = KeySender.VK_RIGHT;
                }
                if ((released & LEFT) != 0) End(ref leftTimer);
                if ((released & RIGHT) != 0) End(ref rightTimer);
            }
            if (tapSpace) send(KeySender.VK_SPACE);
            if (seekDelta != 0) Seek(seekDelta, seekFallback);
        }

        /// <summary>Forget all state, e.g. when the pedal is unplugged mid-press.</summary>
        public void Reset()
        {
            lock (gate)
            {
                StopTimers();
                previous = 0;
            }
        }

        void Begin(ref Timer timer, byte bit, int deltaSeconds, ushort fallbackVk)
        {
            End(ref timer);
            // First repeat after one interval, then steady. Seek itself runs outside the lock
            // so a slow media-session call cannot stall the next pedal report.
            timer = new Timer(delegate
            {
                lock (gate)
                {
                    if (disposed || isPaused() || (previous & bit) == 0) return;
                }
                Seek(deltaSeconds, fallbackVk);
            }, null, RepeatDelayMs, RepeatMs);
        }

        void Seek(int deltaSeconds, ushort fallbackVk)
        {
            if (seekBySeconds != null) seekBySeconds(deltaSeconds);
            else send(fallbackVk);
        }

        void End(ref Timer timer)
        {
            if (timer != null) { timer.Dispose(); timer = null; }
        }

        void StopTimers()
        {
            End(ref leftTimer);
            End(ref rightTimer);
        }

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                StopTimers();
            }
        }
    }
}
