// Integration check that doesn't need a pedal: feeds simulated button states into the real
// Mapper + SendInput code and records what a focused window actually receives.
// Run with helper\tests\run-tests.ps1 (needs an interactive desktop).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace PedalKeys.Tests
{
    class Sink : Form
    {
        public readonly List<string> Keys = new List<string>();
        public readonly Stopwatch Clock = Stopwatch.StartNew();
        public readonly List<long> Times = new List<long>();

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // msg.Msg is WM_KEYDOWN (0x100); lParam bit 24 = extended-key flag.
            bool extended = (((long)msg.LParam >> 24) & 1) != 0;
            Keys.Add(keyData + (extended ? "+ext" : ""));
            Times.Add(Clock.ElapsedMilliseconds);
            return true;
        }
    }

    static class T
    {
        static int failures;

        static void Check(string name, bool ok, string detail)
        {
            Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + "  " + detail);
            if (!ok) failures++;
        }

        static int Count(List<string> keys, string key)
        {
            int n = 0;
            foreach (string k in keys) if (k.StartsWith(key)) n++;
            return n;
        }

        [STAThread]
        static int Main()
        {
            Sink form = new Sink();
            form.Show();
            form.Activate();
            Application.DoEvents();

            bool paused = false;
            Mapper m = new Mapper(delegate { return paused; }, KeySender.Tap);

            Thread script = new Thread(delegate()
            {
                Thread.Sleep(1200);
                // Left held 900 ms -> 1 immediate + first repeat after ~400 ms
                m.Update(1); Thread.Sleep(900); m.Update(0); Thread.Sleep(400);
                // Center held 700 ms -> exactly one Space
                m.Update(2); Thread.Sleep(700); m.Update(0); Thread.Sleep(400);
                // Right held 500 ms -> 1 immediate + 1 repeat
                m.Update(4); Thread.Sleep(500); m.Update(0); Thread.Sleep(400);
                // Paused: nothing fires, even for a pedal held across resume
                paused = true; m.Update(1); Thread.Sleep(300); m.Update(2); m.Update(0); Thread.Sleep(300);
                m.Update(4); paused = false; Thread.Sleep(500); m.Update(0); Thread.Sleep(400);
                form.BeginInvoke(new Action(Application.ExitThread));
            });
            script.Start();
            Application.Run(form);

            List<string> k = form.Keys;
            Console.WriteLine("Received: " + string.Join(", ", k.ToArray()));
            Console.WriteLine("Times   : " + string.Join(", ", form.Times.ConvertAll(t => t.ToString()).ToArray()));

            int left = Count(k, "Left"), space = Count(k, "Space"), right = Count(k, "Right");
            Check("Left press + hold repeat (900ms -> 3-5 taps)", left >= 3 && left <= 5, "got " + left);
            Check("Space fires once per press", space == 1, "got " + space);
            Check("Right press + hold repeat (500ms -> 1-3 taps)", right >= 1 && right <= 3, "got " + right);
            Check("Arrow keys carry the extended-key flag", k.Contains("Left+ext") && k.Contains("Right+ext"), "");
            Check("Nothing fires while paused", left + space + right == k.Count, "total " + k.Count);

            // spacing between repeats of the first Left burst
            List<long> lt = new List<long>();
            for (int i = 0; i < k.Count; i++) if (k[i].StartsWith("Left")) lt.Add(form.Times[i]);
            if (lt.Count >= 2)
            {
                long first = lt[1] - lt[0];
                Check("First repeat after about 400 ms", first >= 300 && first <= 550, first + " ms");
            }
            for (int i = 2; i < lt.Count; i++)
            {
                long gap = lt[i] - lt[i - 1];
                Check("Repeat gap #" + i + " about 200 ms", gap >= 150 && gap <= 300, gap + " ms");
            }
            return failures == 0 ? 0 : 1;
        }
    }
}
