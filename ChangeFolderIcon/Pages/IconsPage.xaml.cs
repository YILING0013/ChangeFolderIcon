using ChangeFolderIcon.Models;
using ChangeFolderIcon.Utils.Events;
using ChangeFolderIcon.Utils.Services;
using ChangeFolderIcon.Utils.WindowsAPI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.Windows.ApplicationModel.Resources;
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
        private readonly ResourceLoader resourceLoader = new();
        private readonly SettingsService? _settingsService;

        // 通知 MainWindow 图标已更改的事件
        public event EventHandler<IconChangedEventArgs>? IconChanged;
        // 请求 MainWindow 导航到设置页面的事件
        public event EventHandler? RequestNavigateToSettings;

        public IconsPage()
        {
            this.InitializeComponent();
            _settingsService = App.SettingsService;
            InitializeIcons();
        }

        /// <summary>
        /// 初始化图标库，如果找不到则显示提示信息
        /// </summary>
        public void InitializeIcons()
        {
            string? icoDir = _settingsService?.Settings?.IconPackPath;
            if (icoDir != null && Directory.Exists(icoDir) && Directory.EnumerateFiles(icoDir, "*.ico").Any())
            {
                // 找到图标，正常加载
                MissingIconsOverlay.Visibility = Visibility.Collapsed;
                IconsGrid.Visibility = Visibility.Visible;
                AlphabetIndexBorder.Visibility = Visibility.Visible;

                Icons.Clear();
                foreach (string ico in Directory.EnumerateFiles(icoDir, "*.ico"))
                {
                    try { Icons.Add(IconInfo.FromPath(ico)); }
                    catch { /* ignore */ }
                }

                SetupGrouping();
                SetupAlphabetIndex();
                UpdateHeaderAndActions();
            }
            else
            {
                // 未找到图标，显示覆盖层提示
                MissingIconsOverlay.Visibility = Visibility.Visible;
                IconsGrid.Visibility = Visibility.Collapsed;
                AlphabetIndexBorder.Visibility = Visibility.Collapsed;
            }
        }

        // 公共方法，用于从外部（如MainWindow）滚动到指定图标
        public void ScrollToIcon(IconInfo? targetIcon)
        {
            if (targetIcon == null) return;

            // 滚动到视图
            IconsGrid.ScrollIntoView(targetIcon, ScrollIntoViewAlignment.Default);

            // 选中该项以高亮显示
            IconsGrid.SelectedItem = targetIcon;
            _selectedIcon = targetIcon;
            UpdateSelectedIconText();
        }

        private void SetupGrouping()
        {
            _groupedCollection?.Clear();

            var groups = Icons.GroupBy(icon =>
            {
                if (string.IsNullOrEmpty(icon.Name)) return "#";
                char firstChar = char.ToUpper(icon.Name[0]);
                if (char.IsDigit(firstChar)) return "0-9";
                if (char.IsLetter(firstChar)) return firstChar.ToString();
                return "#";
            }).OrderBy(g =>
            {
                if (g.Key == "#") return "ZZZ";
                if (g.Key == "0-9") return "000";
                return g.Key;
            });

            foreach (var group in groups)
            {
                _groupedCollection?.Add(new IconGroup(group.Key, group.OrderBy(i => i.Name)));
            }

            var cvs = new CollectionViewSource
            {
                Source = _groupedCollection,
                IsSourceGrouped = true
            };

            GroupedIcons = cvs.View;
        }

        private void SetupAlphabetIndex()
        {
            // 清理旧的按钮以防重复添加
            AlphabetIndexPanel.Children.Clear();

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
                };

                button.Click += (s, e) => OnIndexButtonClick(indexChar);
                AlphabetIndexPanel.Children.Add(button);
            }
        }

        private void OnIndexButtonClick(string index)
        {
            var targetGroup = _groupedCollection?.FirstOrDefault(g => g.Key == index);
            if (targetGroup != null)
            {
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
                HeaderTitle.Text = resourceLoader.GetString("IconPageHeaderTitleText");
                HeaderDescription.Text = resourceLoader.GetString("IconPageHeaderDescriptionText");
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
            SelectedIconText.Text = _selectedIcon != null ? resourceLoader.GetString("Selected") + _selectedIcon.Name : resourceLoader.GetString("IconPageSelectedIconTextText");
        }
        #endregion

        private void IconsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            _selectedIcon = e.ClickedItem as IconInfo;
            UpdateSelectedIconText();
        }

        private void GoToSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            RequestNavigateToSettings?.Invoke(this, EventArgs.Empty);
        }

        #region "单文件夹"按钮操作
        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckPreCondition()) return;
            try
            {
                var progressDialog = ProgressDialog();
                _ = progressDialog.ShowAsync();
                await IconManager.SetFolderIconAsync(_selectedFolderPath!, _selectedIcon!.FullPath);
                progressDialog.Hide();
                IconChanged?.Invoke(this, new IconChangedEventArgs(_selectedFolderPath!, _selectedIcon.FullPath));
                await ShowMsg(resourceLoader.GetString("Done"), resourceLoader.GetString("AppliedFolder"));
            }
            catch (Exception ex) { await ShowMsg(resourceLoader.GetString("Failed"), ex.Message); }
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            { await ShowMsg(resourceLoader.GetString("Note"), resourceLoader.GetString("SelectTargetFolder")); return; }
            try
            {
                await IconManager.ClearFolderIconAsync(_selectedFolderPath!);
                IconChanged?.Invoke(this, new IconChangedEventArgs(_selectedFolderPath!, null));
                await ShowMsg(resourceLoader.GetString("Done"), resourceLoader.GetString("clearedFolder"));
            }
            catch (Exception ex) { await ShowMsg(resourceLoader.GetString("Failed"), ex.Message); }
        }
        #endregion

        #region "递归/批量"按钮操作
        private async void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckPreCondition()) return;
            var progressDialog = ProgressDialog();
            _ = progressDialog.ShowAsync();
            int count = await IconManager.ApplyIconToAllSubfoldersAsync(_selectedFolderPath!, _selectedIcon!.FullPath);
            progressDialog.Hide();
            RaiseForAllSubFolders(_selectedFolderPath!, _selectedIcon!.FullPath);
            await ShowMsg(resourceLoader.GetString("Done"), resourceLoader.GetString("clearedFolderText_1") + count + resourceLoader.GetString("clearedFolderText_2"));
        }

        private async void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            { await ShowMsg(resourceLoader.GetString("Note"), resourceLoader.GetString("SelectTargetFolderFirst")); return; }
            int count = await IconManager.ClearIconRecursivelyAsync(_selectedFolderPath!);
            RaiseForAllSubFolders(_selectedFolderPath!, null);
            await ShowMsg(resourceLoader.GetString("Done"), resourceLoader.GetString("clearedFoldersText_1") + count + resourceLoader.GetString("clearedFoldersText_2"));
        }
        #endregion

        #region 共用小工具
        private bool CheckPreCondition()
        {
            if (_selectedIcon == null)
            {
                _ = ShowMsg(resourceLoader.GetString("Note"), resourceLoader.GetString("ClickToSelect"));
                return false;
            }
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                _ = ShowMsg(resourceLoader.GetString("Note"), resourceLoader.GetString("ClickToSelectLeftNavigation"));
                return false;
            }
            return true;
        }

        private static async Task ShowMsg(string title, string msg) =>
            await new ContentDialog
            {
                Title = title,
                Content = msg,
                CloseButtonText = new ResourceLoader().GetString("CloseButtonText"),
                XamlRoot = App.window?.Content.XamlRoot
            }.ShowAsync();

        private static ContentDialog ProgressDialog() =>
            new ContentDialog
            {
                Title = new ResourceLoader().GetString("ApplyingIcons"),
                Content = new ProgressRing { IsActive = true },
                XamlRoot = App.window?.Content.XamlRoot,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false
            };

    private void RaiseForAllSubFolders(string root, string? iconPath)
        {
            IconChanged?.Invoke(this, new IconChangedEventArgs(root, iconPath));
            foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                IconChanged?.Invoke(this, new IconChangedEventArgs(dir, iconPath));
            }
        }
        #endregion
    }

    public class IconGroup : List<IconInfo>
    {
        public string Key { get; set; }
        public IconGroup(string key, IEnumerable<IconInfo> items) : base(items)
        {
            Key = key;
        }
    }
}