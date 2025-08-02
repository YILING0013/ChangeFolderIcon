using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace ChangeFolderIcon.Utils.Services
{
    public class AppSettings
    {
        public string Language { get; set; } = "en-US";
        public string Theme { get; set; } = "Default"; // "Light", "Dark", "Default"
        public string Backdrop { get; set; } = "Mica"; // "Mica", "Acrylic", "Transparent"

        // 克隆仓库的目标路径
        public string IconRepoLocalPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Assets", "Folder11-ico");

        // 图标文件所在的真实路径 (由仓库路径和ico子目录构成)
        [JsonIgnore] // 此属性不需要保存到json，因为它可以被推导出来
        public string IconPackPath => Path.Combine(IconRepoLocalPath, "ico");

        public string IconPackRepo { get; set; } = "https://github.com/icon11-community/Folder11-ico.git";
    }

    public class SettingsService
    {
        private const string ConfigFileName = "settings.json";
        private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            Settings = LoadSettings();
        }

        // 从文件加载配置
        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // 如果加载失败，返回默认配置
            }
            return new AppSettings();
        }

        // 保存配置到文件
        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception)
            {
                // 处理保存失败的异常
            }
        }

        // 应用主题设置
        public static void ApplyTheme(Window window, string theme)
        {
            if (window.Content is FrameworkElement rootElement)
            {
                switch (theme.ToLower())
                {
                    case "light":
                        rootElement.RequestedTheme = ElementTheme.Light;
                        break;
                    case "dark":
                        rootElement.RequestedTheme = ElementTheme.Dark;
                        break;
                    default:
                        rootElement.RequestedTheme = ElementTheme.Default;
                        break;
                }
            }
        }

        // 应用背景材质设置
        public static void ApplyBackdrop(MainWindow window, string backdrop)
        {
            window.SetBackdrop(backdrop);
        }
    }
}
