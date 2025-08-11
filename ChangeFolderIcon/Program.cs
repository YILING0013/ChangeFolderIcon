using ChangeFolderIcon.Utils.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.Globalization;

namespace ChangeFolderIcon
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            var settingsService = new SettingsService();
            var lang = settingsService.Settings.Language;

            if (!string.IsNullOrWhiteSpace(lang))
            {
                ApplicationLanguages.PrimaryLanguageOverride = lang; // ← Microsoft.Windows.*
            }

            Microsoft.UI.Xaml.Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}
