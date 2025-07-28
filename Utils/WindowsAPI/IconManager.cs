using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileAttributes(string lpFileName, Win32FileAttributes dwFileAttributes);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_UPDATEITEM = 0x00002000;
        private const uint SHCNE_UPDATEDIR = 0x00001000;
        private const uint SHCNF_PATHW = 0x0005; // Use wide-character path
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;


        /// <summary>
        /// 为单个文件夹设置图标。
        /// </summary>
        public static void SetFolderIcon(string folderPath, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(iconPath))
                throw new ArgumentException("Folder path or icon path cannot be empty.");

            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException(folderPath);
            if (!File.Exists(iconPath)) throw new FileNotFoundException(iconPath);

            // 1. 先清除旧设置，确保一个干净的状态。
            ClearFolderIcon(folderPath, notify: false);

            // 2. 将图标文件复制到目标文件夹内，命名为 folder.ico
            string targetIconPath = Path.Combine(folderPath, "folder.ico");
            File.Copy(iconPath, targetIconPath, true);
            SetFileAttributes(targetIconPath, Win32FileAttributes.Hidden | Win32FileAttributes.System);


            // 3. 写入 desktop.ini
            string iniPath = Path.Combine(folderPath, "desktop.ini");
            string iniContent = "[.ShellClassInfo]\r\n" +
                                $"IconResource=folder.ico,0\r\n";
            File.WriteAllText(iniPath, iniContent, Encoding.Unicode);

            // 4. 设置 desktop.ini 和文件夹本身的属性
            SetFileAttributes(iniPath, Win32FileAttributes.Hidden | Win32FileAttributes.System);
            var folderAttrs = (Win32FileAttributes)File.GetAttributes(folderPath);
            SetFileAttributes(folderPath, folderAttrs | Win32FileAttributes.ReadOnly);

            // 5. 通知系统刷新
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

        /// <summary>
        /// 删除/重置单个文件夹图标
        /// </summary>
        public static void ClearFolderIcon(string folderPath, bool notify = true)
        {
            string iniPath = Path.Combine(folderPath, "desktop.ini");
            string iconInFolderPath = Path.Combine(folderPath, "folder.ico");

            // 取消文件夹只读属性以便操作
            var folderAttrs = (Win32FileAttributes)File.GetAttributes(folderPath);
            if (folderAttrs.HasFlag(Win32FileAttributes.ReadOnly))
            {
                SetFileAttributes(folderPath, folderAttrs & ~Win32FileAttributes.ReadOnly);
            }

            // 删除 desktop.ini
            if (File.Exists(iniPath))
            {
                SetFileAttributes(iniPath, Win32FileAttributes.Normal);
                File.Delete(iniPath);
            }

            // 删除文件夹内的图标副本
            if (File.Exists(iconInFolderPath))
            {
                SetFileAttributes(iconInFolderPath, Win32FileAttributes.Normal);
                File.Delete(iconInFolderPath);
            }


            if (notify)
            {
                NotifyExplorer(folderPath);
            }
        }

        /// <summary>
        /// 对子目录递归重置。
        /// </summary>
        public static int ClearIconRecursively(string rootFolderPath)
        {
            if (!Directory.Exists(rootFolderPath)) return 0;
            int count = 0;

            var allDirs = Directory.EnumerateDirectories(rootFolderPath, "*", SearchOption.AllDirectories);

            foreach (var dir in allDirs)
            {
                try { ClearFolderIcon(dir); count++; } catch { /* ignore */ }
            }
            // 自身也清理
            try { ClearFolderIcon(rootFolderPath); count++; } catch { }

            return count;
        }


        private static void NotifyExplorer(string path)
        {
            // 使用 UnmanagedString 类确保非托管内存被正确释放
            using (var pathPtr = new UnmanagedString(path))
            {
                // 通知具体项目更新
                SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pathPtr.Pointer, IntPtr.Zero);
                // 通知目录内容变化
                SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pathPtr.Pointer, IntPtr.Zero);
            }
            // 强制刷新所有关联
            SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// 一个辅助类，用于管理传递给非托管代码的字符串指针，确保内存被释放。
        /// </summary>
        private sealed class UnmanagedString : IDisposable
        {
            public IntPtr Pointer { get; }
            public UnmanagedString(string str) => Pointer = Marshal.StringToHGlobalUni(str);
            public void Dispose() => Marshal.FreeHGlobal(Pointer);
        }
    }
}
