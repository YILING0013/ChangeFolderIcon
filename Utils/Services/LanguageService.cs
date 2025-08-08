using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.Globalization;

namespace ChangeFolderIcon.Utils.Services
{
    public static class LanguageService
    {
        public static void SetLanguage(string language)
        {
            ApplicationLanguages.PrimaryLanguageOverride = language;
            // 重置资源加载器，以便更改生效。要重启应用
        }
    }
}
