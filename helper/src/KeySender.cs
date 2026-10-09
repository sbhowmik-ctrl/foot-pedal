using System;
using System.Runtime.InteropServices;

namespace PedalKeys
{
    /// <summary>Sends real keyboard input to whichever window has focus, via SendInput.</summary>
    internal static class KeySender
    {
        public const ushort VK_SPACE = 0x20;
        public const ushort VK_LEFT = 0x25;
        public const ushort VK_RIGHT = 0x27;

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_SCANCODE = 0x0008;
        const uint MAPVK_VK_TO_VSC = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        // The union must be as large as its largest member (MOUSEINPUT), otherwise
        // sizeof(INPUT) is wrong on 64-bit and SendInput silently rejects the call.
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint code, uint mapType);

        static readonly object gate = new object();

        /// <summary>Presses and releases a key as one atomic tap.</summary>
        public static void Tap(ushort vk)
        {
            uint flags = IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0;
            ushort scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);

            INPUT[] seq = new INPUT[2];
            seq[0] = Make(vk, scan, flags);
            seq[1] = Make(vk, scan, flags | KEYEVENTF_KEYUP);

            lock (gate)
            {
                SendInput((uint)seq.Length, seq, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        // Arrow keys are "extended" keys. Without the flag, Windows can treat them
        // as numpad arrows, which do nothing while NumLock is on.
        static bool IsExtended(ushort vk)
        {
            return vk == VK_LEFT || vk == VK_RIGHT || vk == 0x26 || vk == 0x28;
        }

        static INPUT Make(ushort vk, ushort scan, uint flags)
        {
            INPUT i = new INPUT();
            i.type = INPUT_KEYBOARD;
            i.u.ki.wVk = vk;
            i.u.ki.wScan = scan;
            i.u.ki.dwFlags = flags;
            return i;
        }
    }
}
