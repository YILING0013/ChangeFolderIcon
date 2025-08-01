using ChangeFolderIcon.Models;
using ChangeFolderIcon.Utils.Events;
using ChangeFolderIcon.Utils.WindowsAPI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Pages
{
    public sealed partial class IconsPage : Page
    {
        public ObservableCollection<IconInfo> Icons { get; } = new();
        public ICollectionView? GroupedIcons { get; private set; }

        private string? _selectedFolderPath;
        private IconInfo? _selectedIcon;
        private readonly List<IconGroup>? _groupedCollection = new();

        // 通知 MainWindow 图标已更改的事件
        public event EventHandler<IconChangedEventArgs>? IconChanged;

        public IconsPage()
        {
            this.InitializeComponent();
            LoadIconsFromAssets();
            SetupGrouping();
            SetupAlphabetIndex();
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

        private void SetupGrouping()
        {
            _groupedCollection?.Clear();

            // 优化分组逻辑，将所有数字开头的图标归入“0-9”组
            var groups = Icons.GroupBy(icon =>
            {
                if (string.IsNullOrEmpty(icon.Name)) return "#";
                char firstChar = char.ToUpper(icon.Name[0]);
                if (char.IsDigit(firstChar)) return "0-9"; // 所有数字归为一组
                if (char.IsLetter(firstChar)) return firstChar.ToString();
                return "#";
            }).OrderBy(g =>
            {
                // 优化排序，使数字组在前，#组在后
                if (g.Key == "#") return "ZZZ";
                if (g.Key == "0-9") return "000";
                return g.Key;
            });

            foreach (var group in groups)
            {
                // 为每个分组内的图标也进行排序
                _groupedCollection?.Add(new IconGroup(group.Key, group.OrderBy(i => i.Name)));
            }

            // 创建CollectionViewSource
            var cvs = new CollectionViewSource
            {
                Source = _groupedCollection,
                IsSourceGrouped = true
            };

            GroupedIcons = cvs.View;
        }

        private void SetupAlphabetIndex()
        {
            // 创建索引按钮
            var indexChars = new List<string> { "0-9" };
            indexChars.AddRange(Enumerable.Range('A', 26).Select(i => ((char)i).ToString()));
            indexChars.Add("#");

            foreach (var indexChar in indexChars)
            {
                var button = new Button
                {
                    Content = indexChar,
                    Width = 24,
                    Height = 24,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 2, 0, 2),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    // 如果需要，可以取消注释并确保资源字典中有此样式
                    // Style = (Style)Application.Current.Resources["SubtleButtonStyle"]
                };

                button.Click += (s, e) => OnIndexButtonClick(indexChar);
                AlphabetIndexPanel.Children.Add(button);
            }
        }

        private void OnIndexButtonClick(string index)
        {
            // [修改] 直接使用私有字段 _groupedCollection
            var targetGroup = _groupedCollection?.FirstOrDefault(g => g.Key == index);

            if (targetGroup != null)
            {
                // 直接滚动到分组的标题，而不是某个具体的项
                IconsGrid.ScrollIntoView(targetGroup, ScrollIntoViewAlignment.Leading);
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
                HeaderIcon.Glyph = "\uE946";
                HeaderTitle.Text = "请选择文件夹";
                HeaderDescription.Text = "从左侧导航栏选择文件夹，或直接拖拽文件夹到图标上";
                ActionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeaderIcon.Glyph = "\uE8B7";
                HeaderTitle.Text = Path.GetFileName(_selectedFolderPath);
                HeaderDescription.Text = _selectedFolderPath;
                ActionPanel.Visibility = Visibility.Visible;
            }

            UpdateSelectedIconText();
        }

        private void UpdateSelectedIconText()
        {
            if (_selectedIcon != null)
            {
                SelectedIconText.Text = $"已选择: {_selectedIcon.Name}";
            }
            else
            {
                SelectedIconText.Text = "未选择图标";
            }
        }
        #endregion

        // 当用户在 GridView 中点击一个图标时，更新 _selectedIcon
        private void IconsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            _selectedIcon = e.ClickedItem as IconInfo;
            UpdateSelectedIconText();
        }

        #region "单文件夹"按钮操作
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

        #region "递归/批量"按钮操作
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

    // 辅助类
    public class IconGroup : List<IconInfo>
    {
        public string Key { get; set; }

        public IconGroup(string key, IEnumerable<IconInfo> items) : base(items)
        {
            Key = key;
        }
    }
}