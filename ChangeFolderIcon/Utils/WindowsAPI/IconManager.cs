using ChangeFolderIcon.Utils.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Utils.WindowsAPI
{
    /// <summary>
    ///在文件夹上设置自定义图标的工具类
    /// </summary>
    public static class IconManager
    {
        #region P/Invoke Signatures and Constants

        // --- Win32 P/Invoke for file attributes ---
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileAttributes(string lpFileName, FileAttributes dwFileAttributes);

        // --- Shell P/Invoke for notifications ---
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr ILCreateFromPath(string pszPath);

        [DllImport("ole32.dll", PreserveSig = false)]
        private static extern void CoTaskMemFree(IntPtr pv);

        // --- P/Invoke for the recommended SHGetSetFolderCustomSettings API ---
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHGetSetFolderCustomSettings(ref SHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFOLDERCUSTOMSETTINGS
        {
            public uint dwSize;
            public uint dwMask;
            public IntPtr pvid;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszWebViewTemplate;
            public uint cchWebViewTemplate;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszWebViewTemplateVersion;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszInfoTip;
            public uint cchInfoTip;
            public IntPtr pclsid;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszIconFile;
            public uint cchIconFile;
            public int iIconIndex;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pszLogo;
            public uint cchLogo;
        }

        // --- P/Invoke for broadcasting system-wide setting changes ("nuclear option") ---
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            SendMessageTimeoutFlags fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        [Flags]
        private enum SendMessageTimeoutFlags : uint
        {
            SMTO_NORMAL = 0x0,
            SMTO_BLOCK = 0x1,
            SMTO_ABORTIFHUNG = 0x2,
            SMTO_NOTIMEOUTIFNOTHUNG = 0x8,
            SMTO_ERRORONEXIT = 0x20
        }

        // Constants for SHChangeNotify
        private const uint SHCNE_UPDATEITEM = 0x2000;
        private const uint SHCNE_UPDATEDIR = 0x1000;
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;
        private const uint SHCNF_FLUSH = 0x1000; // Waits for the notification to be processed.

        // Constants for SHGetSetFolderCustomSettings
        private const uint FCSM_ICONFILE = 0x00000010;
        private const uint FCS_FORCEWRITE = 0x00000002; // Forces the write, even if settings are already present.

        // Constants for SendMessageTimeout
        private const uint WM_SETTINGCHANGE = 0x1A;
        private const int SPI_SETICONS = 0x58;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        #endregion

        #region 为单个文件夹设置图标
        /// <summary>
        /// 为单个文件夹设置图标
        /// </summary>
        /// <param name="folderPath">目标文件夹的完整路径</param>
        /// <param name="iconPath">图标文件的完整路径 (.ico)</param>
        private static void SetFolderIconInternal(string folderPath, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("The specified folder does not exist.");
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
                throw new FileNotFoundException("The specified icon file does not exist.");

            ClearOldIconFiles(folderPath);

            string stamp = DateTime.UtcNow.Ticks.ToString();
            string uniqueIconName = $"folder_{stamp}.ico";
            string targetIconPath = Path.Combine(folderPath, uniqueIconName);
            File.Copy(iconPath, targetIconPath, true);
            SetFileAttributes(targetIconPath, FileAttributes.Hidden | FileAttributes.System);

            var fcs = new SHFOLDERCUSTOMSETTINGS
            {
                dwSize = (uint)Marshal.SizeOf<SHFOLDERCUSTOMSETTINGS>(),
                dwMask = FCSM_ICONFILE,
                pszIconFile = targetIconPath,
                cchIconFile = (uint)targetIconPath.Length,
                iIconIndex = 0
            };

            int hr = SHGetSetFolderCustomSettings(ref fcs, folderPath, FCS_FORCEWRITE);
            Marshal.ThrowExceptionForHR(hr);

            var folderAttrs = File.GetAttributes(folderPath);
            SetFileAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);

            NotifyExplorerOfUpdate(folderPath);
        }

        /// <summary>
        /// 清除单个文件夹的自定义图标
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        private static void ClearFolderIconInternal(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return;

            var folderAttrs = File.GetAttributes(folderPath);
            if (folderAttrs.HasFlag(FileAttributes.ReadOnly))
            {
                SetFileAttributes(folderPath, folderAttrs & ~FileAttributes.ReadOnly);
            }

            ClearOldIconFiles(folderPath);

            string iniPath = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(iniPath))
            {
                SetFileAttributes(iniPath, FileAttributes.Normal);
                File.Delete(iniPath);
            }

            NotifyExplorerOfUpdate(folderPath);
        }
        #endregion

        #region 公共调度方法
        /// <summary>
        /// 为单个文件夹设置图标（自动处理权限）
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        /// <param name="iconPath">图标文件的完整路径 (.ico)</param>
        public static async Task SetFolderIconAsync(string folderPath, string iconPath)
        {
            if (ElevationService.NeedsElevation(folderPath))
            {
                // 需要提权
                bool success = await ElevationService.SetFolderIconElevatedAsync(folderPath, iconPath);
                if (!success)
                {
                    throw new Exception("Elevated operation failed or was canceled by the user.");
                }
            }
            else
            {
                await Task.Run(() => SetFolderIconInternal(folderPath, iconPath));
            }
        }

        /// <summary>
        /// 清除单个文件夹的图标（自动处理权限）
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        public static async Task ClearFolderIconAsync(string folderPath)
        {
            if (ElevationService.NeedsElevation(folderPath))
            {
                // 需要提权
                bool success = await ElevationService.ClearFolderIconElevatedAsync(folderPath);
                if (!success)
                {
                    throw new Exception("Elevated operation failed or was canceled by the user.");
                }
            }
            else
            {
                await Task.Run(() => ClearFolderIconInternal(folderPath));
            }
        }

        /// <summary>
        /// 递归地为所有子文件夹应用图标（自动处理权限）
        /// </summary>
        /// <param name="rootFolderPath">根文件夹路径</param>
        /// <param name="iconPath">图标文件的完整路径 (.ico)</param>
        public static async Task<int> ApplyIconToAllSubfoldersAsync(string rootFolderPath, string iconPath)
        {
            if (!Directory.Exists(rootFolderPath)) return 0;

            int applied = 0;
            var allDirs = Directory.EnumerateDirectories(rootFolderPath, "*", SearchOption.AllDirectories).ToList();
            // 包含根目录自身
            allDirs.Insert(0, rootFolderPath);

            foreach (var dir in allDirs)
            {
                try
                {
                    await SetFolderIconAsync(dir, iconPath);
                    applied++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to set icon for {dir}: {ex.Message}");
                }
            }
            return applied;
        }

        /// <summary>
        /// 递归地清除所有子文件夹的图标（自动处理权限）
        /// </summary>
        /// <param name="rootFolderPath">根文件夹路径</param>
        public static async Task<int> ClearIconRecursivelyAsync(string rootFolderPath)
        {
            if (!Directory.Exists(rootFolderPath)) return 0;
            int count = 0;

            var allDirs = Directory.EnumerateDirectories(rootFolderPath, "*", SearchOption.AllDirectories).ToList();
            // 包含根目录自身
            allDirs.Insert(0, rootFolderPath);

            foreach (var dir in allDirs)
            {
                try
                {
                    await ClearFolderIconAsync(dir);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clear icon for {dir}: {ex.Message}");
                }
            }
            return count;
        }
        #endregion

        #region 私有辅助方法
        private static void NotifyExplorerOfUpdate(string path)
        {
            IntPtr pidl = IntPtr.Zero;
            try
            {
                pidl = ILCreateFromPath(path);
                if (pidl == IntPtr.Zero) return;

                const uint flags = SHCNF_IDLIST | SHCNF_FLUSH;
                SHChangeNotify(SHCNE_UPDATEITEM, flags, pidl, IntPtr.Zero);
                SHChangeNotify(SHCNE_UPDATEDIR, flags, pidl, IntPtr.Zero);
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                if (pidl != IntPtr.Zero)
                {
                    CoTaskMemFree(pidl);
                }
            }

            RefreshIconCacheViaIe4uinit();

            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, (IntPtr)SPI_SETICONS, IntPtr.Zero, SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, 1000, out _);
        }

        private static void ClearOldIconFiles(string folderPath)
        {
            try
            {
                var oldIcons = Directory.EnumerateFiles(folderPath, "folder_*.ico");
                foreach (var oldIcon in oldIcons)
                {
                    SetFileAttributes(oldIcon, FileAttributes.Normal);
                    File.Delete(oldIcon);
                }
                string legacyIconPath = Path.Combine(folderPath, "folder.ico");
                if (File.Exists(legacyIconPath))
                {
                    SetFileAttributes(legacyIconPath, FileAttributes.Normal);
                    File.Delete(legacyIconPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing old icon files in {folderPath}: {ex.Message}");
            }
        }

        private static void RefreshIconCacheViaIe4uinit()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ie4uinit.exe",
                    Arguments = "-show",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo)?.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to run ie4uinit.exe: {ex.Message}");
            }
        }
        #endregion
    }
}