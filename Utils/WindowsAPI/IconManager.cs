using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Utils.WindowsAPI
{
    public static class IconManager
    {
        // --- Win32 P/Invoke ---
        [Flags]
        private enum Win32FileAttributes : uint
        {
            ReadOnly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Normal = 0x00000080,
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetFileAttributes(string lpFileName, Win32FileAttributes dwFileAttributes);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(
            uint wEventId,
            uint uFlags,
            IntPtr dwItem1,
            IntPtr dwItem2);

        private const uint SHCNE_UPDATEITEM = 0x00002000;
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATHW = 0x0005;

        /// <summary>
        /// 为单个文件夹设置图标。
        /// </summary>
        public static void SetFolderIcon(string folderPath, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(iconPath))
                throw new ArgumentException("folderPath 或 iconPath 为空。");

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException(folderPath);

            if (!File.Exists(iconPath))
                throw new FileNotFoundException(iconPath);

            // 1) 写 desktop.ini
            var iniPath = Path.Combine(folderPath, "desktop.ini");
            var iniContent =
@"[.ShellClassInfo]
IconResource=" + iconPath + ",0";

            File.WriteAllText(iniPath, iniContent);

            // 2) 设置 desktop.ini 为隐藏+系统（使用 Win32 枚举）
            SetFileAttributes(iniPath, Win32FileAttributes.Hidden | Win32FileAttributes.System);

            // 3) 设置文件夹为只读（使用 .NET 的 System.IO.FileAttributes）
            var attrs = File.GetAttributes(folderPath);
            if (!attrs.HasFlag(System.IO.FileAttributes.ReadOnly))
            {
                File.SetAttributes(folderPath, attrs | System.IO.FileAttributes.ReadOnly);
            }

            // 4) 通知系统刷新图标显示
            NotifyExplorer(folderPath);
        }

        /// <summary>
        /// 递归应用图标到指定文件夹的所有子文件夹（不含自身）。
        /// </summary>
        public static int ApplyIconToAllSubfolders(string rootFolderPath, string iconPath)
        {
            if (!Directory.Exists(rootFolderPath)) return 0;

            int applied = 0;
            foreach (var sub in Directory.EnumerateDirectories(rootFolderPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    SetFolderIcon(sub, iconPath);
                    applied++;
                }
                catch
                {
                    // 忽略单个失败，继续
                }
            }
            return applied;
        }

        private static void NotifyExplorer(string path)
        {
            using var pathPtr = new UnmanagedString(path);
            SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pathPtr.Pointer, IntPtr.Zero);
            SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pathPtr.Pointer, IntPtr.Zero);
        }

        private sealed class UnmanagedString : IDisposable
        {
            public IntPtr Pointer { get; }
            public UnmanagedString(string str) => Pointer = Marshal.StringToHGlobalUni(str);
            public void Dispose() => Marshal.FreeHGlobal(Pointer);
        }
    }
}
