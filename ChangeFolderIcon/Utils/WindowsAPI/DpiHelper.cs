using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Utils.WindowsAPI
{
    public class DpiHelper
    {
        // Win32 API for DPI
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetDpiForWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

        // Constants for system metrics
        private const int SM_CXCAPTION = 4;
        private const int SM_CYCAPTION = 4;

        /// <summary>
        /// 获取当前窗口的DPI缩放比例
        /// </summary>
        public static double GetScaleAdjustment(Window window)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var dpi = GetDpiForWindow(hWnd);
            return (double)dpi / 96;
        }
    }
}
