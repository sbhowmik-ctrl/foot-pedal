using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PedalKeys
{
    /// <summary>
    /// Thin wrapper over the Windows HID API (hid.dll / cfgmgr32.dll / kernel32.dll).
    /// This is the same API that hidapi and node-hid use on Windows, called directly
    /// so the helper has no native dependencies to ship.
    /// </summary>
    internal static class Hid
    {
        const uint GENERIC_READ = 0x80000000;
        const uint FILE_SHARE_READ = 1;
        const uint FILE_SHARE_WRITE = 2;
        const uint OPEN_EXISTING = 3;
        const int CR_SUCCESS = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES attrs);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);

        [DllImport("hid.dll", SetLastError = true)]
        static extern bool HidD_FreePreparsedData(IntPtr data);

        [DllImport("hid.dll")]
        static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        static extern int CM_Get_Device_Interface_List_Size(out int len, ref Guid guid, string deviceId, uint flags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        static extern int CM_Get_Device_Interface_List(ref Guid guid, string deviceId, char[] buffer, int len, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
            uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(SafeFileHandle h, byte[] buffer, int toRead, out int read, IntPtr overlapped);

        /// <summary>Device paths of every present HID interface matching the vendor/product id.</summary>
        public static List<string> FindPaths(ushort vid, ushort pid)
        {
            List<string> found = new List<string>();
            foreach (string path in EnumeratePaths())
            {
                // Access 0 is enough to ask for attributes, and works even for
                // collections Windows keeps exclusive (keyboards, mice).
                using (SafeFileHandle h = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                {
                    if (h.IsInvalid) continue;
                    HIDD_ATTRIBUTES a = new HIDD_ATTRIBUTES();
                    a.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                    if (HidD_GetAttributes(h, ref a) && a.VendorID == vid && a.ProductID == pid)
                        found.Add(path);
                }
            }
            return found;
        }

        static IEnumerable<string> EnumeratePaths()
        {
            Guid guid;
            HidD_GetHidGuid(out guid);
            int len;
            if (CM_Get_Device_Interface_List_Size(out len, ref guid, null, 0) != CR_SUCCESS || len <= 1)
                yield break;
            // A multi-string: path\0path\0\0. A StringBuilder stops at the first null, which
            // hid every device after the first one (the pedal included).
            char[] buffer = new char[len];
            if (CM_Get_Device_Interface_List(ref guid, null, buffer, len, 0) != CR_SUCCESS)
                yield break;
            foreach (string p in new string(buffer).Split('\0'))
                if (p.Length > 0) yield return p;
        }

        /// <summary>An open input-report stream for one HID interface.</summary>
        public sealed class Reader : IDisposable
        {
            readonly SafeFileHandle handle;
            readonly byte[] buffer;

            Reader(SafeFileHandle h, int reportLength)
            {
                handle = h;
                buffer = new byte[reportLength];
            }

            /// <summary>Returns null if the interface can't be opened for reading or has no input reports.</summary>
            public static Reader Open(string path)
            {
                SafeFileHandle h = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.IsInvalid) { h.Dispose(); return null; }

                IntPtr pre;
                if (!HidD_GetPreparsedData(h, out pre)) { h.Dispose(); return null; }
                HIDP_CAPS caps;
                int status = HidP_GetCaps(pre, out caps);
                HidD_FreePreparsedData(pre);
                if (status < 0 || caps.InputReportByteLength == 0) { h.Dispose(); return null; }

                return new Reader(h, caps.InputReportByteLength);
            }

            /// <summary>
            /// Blocks until the next input report. The returned array holds the report
            /// payload; the leading report-id byte Windows adds is already stripped.
            /// Returns null when the device is gone.
            /// </summary>
            public byte[] Read()
            {
                int n;
                if (!ReadFile(handle, buffer, buffer.Length, out n, IntPtr.Zero) || n < 2) return null;
                byte[] data = new byte[n - 1];
                Array.Copy(buffer, 1, data, 0, n - 1);
                return data;
            }

            public void Dispose() { handle.Dispose(); }
        }
    }
}
