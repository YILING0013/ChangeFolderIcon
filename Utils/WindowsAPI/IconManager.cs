using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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
        private const uint FCS_CLEAR = 0x00000004; // Clears custom settings.

        // Constants for SendMessageTimeout
        private const uint WM_SETTINGCHANGE = 0x1A;
        private const int SPI_SETICONS = 0x58;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        #endregion

        /// <summary>
        /// 为单个文件夹设置图标，采用多种策略确保立即刷新
        /// </summary>
        /// <param name="folderPath">目标文件夹的完整路径</param>
        /// <param name="iconPath">图标文件的完整路径 (.ico)</param>
        public static void SetFolderIcon(string folderPath, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("The specified folder does not exist.");
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
                throw new FileNotFoundException("The specified icon file does not exist.");

            // 1. 清理旧的图标文件
            ClearOldIconFiles(folderPath);

            // 2.为了破坏缓存，复制图标文件到目标文件夹并使用唯一的时间戳命名
            string stamp = DateTime.UtcNow.Ticks.ToString();
            string uniqueIconName = $"folder_{stamp}.ico";
            string targetIconPath = Path.Combine(folderPath, uniqueIconName);
            File.Copy(iconPath, targetIconPath, true);
            SetFileAttributes(targetIconPath, FileAttributes.Hidden | FileAttributes.System);

            // 3.使用 SHGetSetFolderCustomSettings API
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
        /// 异步为单个文件夹设置图标，采用多种策略确保立即刷新
        /// </summary>
        /// <param name="folderPath">目标文件夹的完整路径</param>
        /// <param name="iconPath">图标文件的完整路径 (.ico)</param>
        public static Task SetFolderIconAsync(string folderPath, string iconPath)
        {
            return Task.Run(() =>
            {
                SetFolderIcon(folderPath, iconPath);
            });
        }

        /// <summary>
        /// 删除/重置单个文件夹的自定义图标
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        public static void ClearFolderIcon(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return;

            // 1. 取消文件夹的只读属性，以便删除文件
            var folderAttrs = File.GetAttributes(folderPath);
            if (folderAttrs.HasFlag(FileAttributes.ReadOnly))
            {
                SetFileAttributes(folderPath, folderAttrs & ~FileAttributes.ReadOnly);
            }

            // 2. 清理旧的图标文件
            ClearOldIconFiles(folderPath);

            // 3. 删除 desktop.ini 文件
            string iniPath = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(iniPath))
            {
                SetFileAttributes(iniPath, FileAttributes.Normal);
                File.Delete(iniPath);
            }

            // 4. 通知系统更新
            NotifyExplorerOfUpdate(folderPath);
        }

        /// <summary>
        /// 异步删除/重置单个文件夹的自定义图标
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        public static Task ClearFolderIconAsync(string folderPath)
        {
            return Task.Run(() => {
                ClearFolderIcon(folderPath);
            });
        }

        /// <summary>
        /// 递归地为指定文件夹下的所有子文件夹应用图标
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
                catch (Exception ex)
                {
                    // Log or handle individual failures
                    Debug.WriteLine($"Failed to set icon for {sub}: {ex.Message}");
                }
            }
            return applied;
        }

        /// <summary>
        /// 异步递归地为指定文件夹下的所有子文件夹应用图标
        /// </summary>
        public static Task<int> ApplyIconToAllSubfoldersAsync(string rootFolderPath, string iconPath)
        {
            return Task.Run(() => {
                return ApplyIconToAllSubfolders(rootFolderPath, iconPath);
            });
        }

        /// <summary>
        /// 递归地清除指定文件夹及其所有子文件夹的自定义图标
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
            // 也清理根目录自身
            try { ClearFolderIcon(rootFolderPath); count++; } catch { /* ignore */ }

            return count;
        }

        /// <summary>
        /// 异步递归地清除指定文件夹及其所有子文件夹的自定义图标
        /// </summary>
        public static Task<int> ClearIconRecursivelyAsync(string rootFolderPath)
        {
            return Task.Run(() => {
                return ClearIconRecursively(rootFolderPath);
            });
        }

        /// <summary>
        /// 多层次的通知以确保 Explorer 图标被刷新
        /// </summary>
        private static void NotifyExplorerOfUpdate(string path)
        {
            IntPtr pidl = IntPtr.Zero;
            try
            {
                pidl = ILCreateFromPath(path);
                if (pidl == IntPtr.Zero) return;

                // 1.SHCNF_IDLIST | SHCNF_FLUSH 发送同步通知
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

            // 2.调用 ie4uinit.exe -show
            RefreshIconCacheViaIe4uinit();

            // 3.广播 WM_SETTINGCHANGE 消息，强制所有程序重载系统图标
            SendMessageTimeout(
                HWND_BROADCAST,
                WM_SETTINGCHANGE,
                (IntPtr)SPI_SETICONS,
                IntPtr.Zero,
                SendMessageTimeoutFlags.SMTO_ABORTIFHUNG,
                1000,
                out _);
        }

        /// <summary>
        /// 删除文件夹内所有先前生成的图标文件
        /// </summary>
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
                // Also handle the legacy "folder.ico"
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

        /// <summary>
        /// 调用 ie4uinit.exe 来强制刷新图标缓存
        /// </summary>
        private static void RefreshIconCacheViaIe4uinit()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ie4uinit.exe",
                    // For Win 10/11, use -show. For older systems, -ClearIconCache might be used.
                    Arguments = "-show",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo)?.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                // This can fail if the exe is not found or permissions are insufficient.
                // It's an optional step, so we just log the error and continue.
                Debug.WriteLine($"Failed to run ie4uinit.exe: {ex.Message}");
            }
        }
    }
}
