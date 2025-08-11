using ChangeFolderIcon.Utils.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.IO;
using System.Linq;

namespace ChangeFolderIcon.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsService? _settingsService;
        private readonly IconPackService? _iconPackService;
        private bool _isInitializing = true;
        private readonly ResourceLoader resourceLoader = new();

        // 当图标包路径改变时触发的事件
        public event EventHandler? IconPackPathChanged;

        public SettingsPage()
        {
            this.InitializeComponent();
            _settingsService = App.SettingsService;
            _iconPackService = App.IconPackService;

            if (_iconPackService != null)
            {
                _iconPackService.StatusUpdated += OnIconPackStatusUpdated;
            }

            LoadSettings();
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Language
            var currentLang = _settingsService?.Settings.Language;
            LanguageComboBox.SelectedItem = LanguageComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Tag == currentLang) ?? LanguageComboBox.Items[0];

            // Theme
            var currentTheme = _settingsService?.Settings.Theme;
            ThemeComboBox.SelectedItem = ThemeComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Tag == currentTheme) ?? ThemeComboBox.Items[2];

            // Backdrop
            var currentBackdrop = _settingsService?.Settings.Backdrop;
            BackdropComboBox.SelectedItem = BackdropComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Tag == currentBackdrop) ?? BackdropComboBox.Items[0];

            // Icon Pack Path
            UpdateIconPackPathDisplay();
        }

        private void UpdateIconPackPathDisplay()
        {
            if (_settingsService != null)
            {
                // 显示当前设置的ico文件夹路径
                string path = _settingsService.Settings.IconPackPath ?? resourceLoader.GetString("NotSet");
                string displayPath = _settingsService.Settings.IconRepoLocalPath ?? resourceLoader.GetString("NotSet");
                IconPackPathTextBlock.Text = $"{resourceLoader.GetString("CurrentPath")}: {displayPath}";
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedTag = ((ComboBoxItem)LanguageComboBox.SelectedItem)?.Tag as string;
            if (selectedTag != null && _settingsService != null)
            {
                // Check if language actually changed
                if (_settingsService.Settings.Language != selectedTag)
                {
                    _settingsService.Settings.Language = selectedTag;
                    _settingsService.SaveSettings();
                    LanguageService.SetLanguage(selectedTag);

                    // Show restart info bar
                    LanguageRestartInfoBar.IsOpen = true;
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedTag = ((ComboBoxItem)ThemeComboBox.SelectedItem)?.Tag as string;
            if (selectedTag != null && _settingsService != null)
            {
                _settingsService.Settings.Theme = selectedTag;
                _settingsService.SaveSettings();

                if (App.window != null)
                {
                    SettingsService.ApplyTheme(App.window, selectedTag);
                }
            }
        }

        private void BackdropComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedTag = ((ComboBoxItem)BackdropComboBox.SelectedItem)?.Tag as string;
            if (selectedTag != null && _settingsService != null)
            {
                if (App.window is MainWindow mainWindow)
                {
                    _settingsService.Settings.Backdrop = selectedTag;
                    _settingsService.SaveSettings();
                    SettingsService.ApplyBackdrop(mainWindow, selectedTag);
                }
            }
        }

        private async void UpdateIconsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_iconPackService == null || _settingsService == null) return;

            UpdateIconsButton.IsEnabled = false;
            UpdateProgressRing.Visibility = Visibility.Visible;
            IconPackStatusTextBlock.Text = "";

            try
            {
                bool success = await _iconPackService.UpdateIconPackAsync();
                if (success && _iconPackService.IconPackDirectory != null)
                {
                    _settingsService.Settings.IconRepoLocalPath = _iconPackService.IconPackDirectory;
                    _settingsService.SaveSettings();
                    UpdateIconPackPathDisplay();
                    IconPackPathChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                IconPackStatusTextBlock.Text = $"Error: {ex.Message}";
            }
            finally
            {
                UpdateIconsButton.IsEnabled = true;
                UpdateProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void SelectIconsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            picker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null && _settingsService != null)
            {
                _settingsService.Settings.IconRepoLocalPath = folder.Path;
                _settingsService.SaveSettings();
                UpdateIconPackPathDisplay();
                // 触发事件通知MainWindow刷新IconsPage
                IconPackPathChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnIconPackStatusUpdated(string status)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                IconPackStatusTextBlock.Text = status;
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Unsubscribe from events
            if (_iconPackService != null)
            {
                _iconPackService.StatusUpdated -= OnIconPackStatusUpdated;
            }
        }
    }
}