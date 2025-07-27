using ChangeFolderIcon.Models;
using ChangeFolderIcon.Utils.WindowsAPI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ChangeFolderIcon.Pages
{
    public sealed partial class IconsPage : Page
    {
        public ObservableCollection<IconInfo> Icons { get; } = new();

        private string? _selectedFolderPath;  // 来自 MainWindow 的当前选中文件夹
        private IconInfo? _selectedIcon;      // 当前在图标库中选中的图标

        public IconsPage()
        {
            this.InitializeComponent();
            LoadIconsFromAssets();
            UpdateHeaderAndActions();
        }

        private void LoadIconsFromAssets()
        {
            // 约定：Assets/ico 位于程序根目录
            var baseDir = AppContext.BaseDirectory;
            var icoDir = Path.Combine(baseDir, "Assets", "ico");
            if (!Directory.Exists(icoDir)) return;

            foreach (var ico in Directory.EnumerateFiles(icoDir, "*.ico"))
            {
                try { Icons.Add(IconInfo.FromPath(ico)); }
                catch { /* 忽略坏图标 */ }
            }
        }

        /// <summary> 由 MainWindow 调用，更新“是否选中文件夹”的状态。 </summary>
        public void UpdateState(string? selectedFolderPath)
        {
            _selectedFolderPath = selectedFolderPath;
            UpdateHeaderAndActions();
        }

        private void UpdateHeaderAndActions()
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                HeaderText.Text = "选择一个喜欢的图标，拖拽外部文件夹到图标进行应用。";
                ActionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeaderText.Text = $"已选择文件夹：{_selectedFolderPath}\n选择一个喜欢的图标并应用。";
                ActionPanel.Visibility = Visibility.Visible;
            }
        }

        private void IconsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            _selectedIcon = e.ClickedItem as IconInfo;
        }

        // 页面根的拖拽支持（可选增强）：在页面空白处也能投递文件夹
        private void Page_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = _selectedIcon == null
                    ? "请先选择一个图标"
                    : "释放以将当前图标应用到拖入的文件夹";
            }
        }

        private async void Page_Drop(object sender, DragEventArgs e)
        {
            if (_selectedIcon == null)
            {
                var dlg = new ContentDialog
                {
                    Title = "提示",
                    Content = "请先在上方图标库中选择一个图标。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();
            var folders = items.OfType<StorageFolder>().ToList();
            if (folders.Count == 0) return;

            int ok = 0, fail = 0;
            foreach (var f in folders)
            {
                try { IconManager.SetFolderIcon(f.Path, _selectedIcon.FullPath); ok++; }
                catch { fail++; }
            }

            var resultDlg = new ContentDialog
            {
                Title = "应用结果",
                Content = $"成功：{ok}，失败：{fail}",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await resultDlg.ShowAsync();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIcon == null || string.IsNullOrEmpty(_selectedFolderPath))
            {
                var dlg = new ContentDialog
                {
                    Title = "提示",
                    Content = "请选择一个图标，并在左侧导航中选择目标文件夹。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
                return;
            }

            try
            {
                IconManager.SetFolderIcon(_selectedFolderPath!, _selectedIcon.FullPath);
                await new ContentDialog
                {
                    Title = "完成",
                    Content = "已应用到选中文件夹。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
            }
        }

        private async void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIcon == null || string.IsNullOrEmpty(_selectedFolderPath))
            {
                var dlg = new ContentDialog
                {
                    Title = "提示",
                    Content = "请选择一个图标，并在左侧导航中选择目标文件夹。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
                return;
            }

            int count = IconManager.ApplyIconToAllSubfolders(_selectedFolderPath!, _selectedIcon.FullPath);

            await new ContentDialog
            {
                Title = "完成",
                Content = $"已为 {count} 个子文件夹应用图标。",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                var dlg = new ContentDialog
                {
                    Title = "提示",
                    Content = "请在左侧导航中选择一个目标文件夹。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
                return;
            }
            try
            {
                IconManager.ClearFolderIcon(_selectedFolderPath!);
                await new ContentDialog
                {
                    Title = "完成",
                    Content = $"已清除该文件夹的图标。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
            }
        }

        private async void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                var dlg = new ContentDialog
                {
                    Title = "提示",
                    Content = "请在左侧导航中选择一个目标文件夹。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
                return;
            }
            try
            {
                int count = IconManager.ClearIconRecursively(_selectedFolderPath!);
                await new ContentDialog
                {
                    Title = "完成",
                    Content = $"已清除 {count} 个子文件夹的图标。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
            }
        }
    }
}
