using ChangeFolderIcon.Utils.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace ChangeFolderIcon.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsService? _settingsService;
        private readonly IconPackService? _iconPackService;
        private bool _isInitializing = true;

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
            ThemeRadioButtons.SelectedItem = ThemeRadioButtons.Items
                .Cast<RadioButton>()
                .FirstOrDefault(rb => (string)rb.Tag == currentTheme) ?? ThemeRadioButtons.Items[2];

            // Backdrop
            var currentBackdrop = _settingsService?.Settings.Backdrop;
            BackdropRadioButtons.SelectedItem = BackdropRadioButtons.Items
                .Cast<RadioButton>()
                .FirstOrDefault(rb => (string)rb.Tag == currentBackdrop) ?? BackdropRadioButtons.Items[0];
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

        private void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedTag = ((RadioButton)ThemeRadioButtons.SelectedItem)?.Tag as string;
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

        private void BackdropRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedTag = ((RadioButton)BackdropRadioButtons.SelectedItem)?.Tag as string;
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
            if (_iconPackService == null) return;

            UpdateIconsButton.IsEnabled = false;
            UpdateProgressRing.Visibility = Visibility.Visible;
            IconPackStatusTextBlock.Text = "";

            try
            {
                await _iconPackService.UpdateIconPackAsync();
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