using ChangeFolderIcon.Models;
using ChangeFolderIcon.Utils.Events;
using ChangeFolderIcon.Utils.WindowsAPI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Pages
{
    public sealed partial class IconsPage : Page
    {
        public ObservableCollection<IconInfo> Icons { get; } = new();

        private string? _selectedFolderPath;
        private IconInfo? _selectedIcon;

        // 通知 MainWindow 图标已更改的事件
        public event EventHandler<IconChangedEventArgs>? IconChanged;

        public IconsPage()
        {
            InitializeComponent();
            LoadIconsFromAssets();
            UpdateHeaderAndActions();
        }

        private void LoadIconsFromAssets()
        {
            string baseDir = AppContext.BaseDirectory;
            string icoDir = Path.Combine(baseDir, "Assets", "ico");
            if (!Directory.Exists(icoDir)) return;

            foreach (string ico in Directory.EnumerateFiles(icoDir, "*.ico"))
            {
                try { Icons.Add(IconInfo.FromPath(ico)); }
                catch { /* ignore */ }
            }
        }

        // 由 MainWindow 调用以更新所选文件夹的状态
        public void UpdateState(string? selectedFolderPath)
        {
            _selectedFolderPath = selectedFolderPath;
            UpdateHeaderAndActions();
        }

        #region UI 状态
        private void UpdateHeaderAndActions()
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                HeaderText.Text = "将外部文件夹拖到一个图标上以应用样式，或从左侧选择一个文件夹后使用下方按钮操作。";
                ActionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeaderText.Text = $"已选择文件夹：{_selectedFolderPath}\n选择一个图标并点击下方按钮应用。";
                ActionPanel.Visibility = Visibility.Visible;
            }
        }
        #endregion

        // 当用户在 GridView 中点击一个图标时，更新 _selectedIcon
        private void IconsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            _selectedIcon = e.ClickedItem as IconInfo;
        }

        #region “单文件夹”按钮操作
        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckPreCondition()) return;

            try
            {
                IconManager.SetFolderIcon(_selectedFolderPath!, _selectedIcon!.FullPath);
                IconChanged?.Invoke(this, new IconChangedEventArgs(_selectedFolderPath!, _selectedIcon.FullPath));
                await ShowMsg("完成", "已应用到选中文件夹。");
            }
            catch (Exception ex) { await ShowMsg("失败", ex.Message); }
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            { await ShowMsg("提示", "请在左侧导航中选择一个目标文件夹。"); return; }

            try
            {
                IconManager.ClearFolderIcon(_selectedFolderPath!);
                IconChanged?.Invoke(this, new IconChangedEventArgs(_selectedFolderPath!, null));
                await ShowMsg("完成", "已清除该文件夹的图标。");
            }
            catch (Exception ex) { await ShowMsg("失败", ex.Message); }
        }
        #endregion

        #region “递归/批量”按钮操作
        private async void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckPreCondition()) return;

            int count = IconManager.ApplyIconToAllSubfolders(_selectedFolderPath!, _selectedIcon!.FullPath);
            RaiseForAllSubFolders(_selectedFolderPath!, _selectedIcon!.FullPath);
            await ShowMsg("完成", $"已为 {count} 个子文件夹应用图标。");
        }

        private async void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            { await ShowMsg("提示", "请先选择目标文件夹。"); return; }

            int count = IconManager.ClearIconRecursively(_selectedFolderPath!);
            RaiseForAllSubFolders(_selectedFolderPath!, null);
            await ShowMsg("完成", $"已清除 {count} 个文件夹（包括子文件夹）的图标。");
        }
        #endregion

        #region 共用小工具
        private bool CheckPreCondition()
        {
            if (_selectedIcon == null)
            {
                _ = ShowMsg("提示", "请先点击选择一个图标。");
                return false;
            }
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                _ = ShowMsg("提示", "请先在左侧导航中选择一个文件夹。");
                return false;
            }
            return true;
        }

        private static async Task ShowMsg(string title, string msg) =>
            await new ContentDialog
            {
                Title = title,
                Content = msg,
                CloseButtonText = "确定",
                XamlRoot = App.window?.Content.XamlRoot
            }.ShowAsync();

        private void RaiseForAllSubFolders(string root, string? iconPath)
        {
            // 根目录本身
            IconChanged?.Invoke(this, new IconChangedEventArgs(root, iconPath));
            // 所有子目录
            foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                IconChanged?.Invoke(this, new IconChangedEventArgs(dir, iconPath));
            }
        }
        #endregion
    }
}
