using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PedalKeys
{
    /// <summary>"Start with Windows" via HKCU\...\Run. Per-user, no administrator rights needed.</summary>
    internal static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppKey = @"Software\PedalKeys";
        const string ValueName = "PedalKeys";

        public static bool IsEnabled
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(ValueName, false);
            }
        }

        /// <summary>
        /// Start-with-Windows is on by default. We apply that default exactly once, so a user
        /// who turns it off in the tray menu isn't silently switched back on at next launch.
        /// </summary>
        public static void ApplyDefaultOnFirstRun()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(AppKey))
            {
                if (k.GetValue("Initialized") != null) return;
                SetEnabled(true);
                k.SetValue("Initialized", 1, RegistryValueKind.DWord);
            }
        }
    }
}
