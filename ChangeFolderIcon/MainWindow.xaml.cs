using ChangeFolderIcon.Models;
using ChangeFolderIcon.Pages;
using ChangeFolderIcon.Utils.Events;
using ChangeFolderIcon.Utils.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ChangeFolderIcon
{
    // 定义搜索结果模型
    public class SearchResult
    {
        public string? DisplayName { get; set; }
        public string? Path { get; set; } // 文件夹路径或图标路径
        public SearchResultType Type { get; set; }
        public object? OriginalItem { get; set; } // 原始对象 (IconInfo 或 NavigationViewItem)

        // 根据类型返回不同的图标符号
        public string IconGlyph => Type == SearchResultType.Folder ? "\uE8B7" : "\uE7B8";
    }

    public enum SearchResultType
    {
        Folder,
        Icon
    }

    public sealed partial class MainWindow : Window
    {
        private readonly FolderNavigationService? _folderService = new();
        private IconsPage? _iconsPage; // 改为可替换的字段
        private readonly SettingsPage? _settingsPage;
        private readonly ResourceLoader? resourceLoader = new();

        public MainWindow()
        {
            this.InitializeComponent();

            // 初始化设置页面并订阅其事件
            _settingsPage = new SettingsPage();
            _settingsPage.IconPackPathChanged += OnIconPackPathChanged;

            // 创建初始的图标页面
            CreateAndNavigateToIconsPage();

            // 设置窗口的其余部分
            NavView.PaneOpening += NavView_PaneStateChanged;
            NavView.PaneClosing += NavView_PaneStateChanged;
            UpdatePaneVisibility(NavView.IsPaneOpen);

            // 订阅标题栏搜索框的事件
            CustomTitleBarControl.TextChanged += CustomTitleBar_TextChanged;
            CustomTitleBarControl.SuggestionChosen += CustomTitleBar_SuggestionChosen;
            CustomTitleBarControl.QuerySubmitted += CustomTitleBar_QuerySubmitted;

            CustomTitleBarControl.SettingsClicked += OnSettingsClicked;
        }

        /// <summary>
        /// 创建或重新创建IconsPage，并处理事件订阅
        /// </summary>
        private void CreateAndNavigateToIconsPage()
        {
            // 如果存在旧实例，则取消订阅其事件
            if (_iconsPage != null)
            {
                _iconsPage.IconChanged -= IconsPage_IconChanged;
                _iconsPage.RequestNavigateToSettings -= OnRequestNavigateToSettings;
            }

            // 在页面重建期间解绑SelectionChanged事件
            NavView.SelectionChanged -= NavView_SelectionChanged;

            _iconsPage = new IconsPage();
            _iconsPage.IconChanged += IconsPage_IconChanged;
            _iconsPage.RequestNavigateToSettings += OnRequestNavigateToSettings;
            ContentFrame.Content = _iconsPage;

            string? path = (NavView.SelectedItem as NavigationViewItem)?.Tag as string;
            _iconsPage.UpdateState(path);

            // 重绑定SelectionChanged
            NavView.SelectionChanged += NavView_SelectionChanged;
            NavView.IsPaneVisible = true;
        }

        /// <summary>
        /// 当图标包在设置中更新时触发
        /// </summary>
        private void OnIconPackPathChanged(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // 重新创建图标页面以加载新图标
                CreateAndNavigateToIconsPage();

                // 如果当前在设置页面，则自动导航回新的图标页面
                if (ContentFrame.Content is SettingsPage)
                {
                    ContentFrame.Content = _iconsPage;
                }
            });
        }

        /// <summary>
        /// 当图标页面请求导航到设置时触发
        /// </summary>
        private void OnRequestNavigateToSettings(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ContentFrame.Content = _settingsPage;
                NavView.IsPaneVisible = false;
            });
        }


        #region 搜索逻辑
        // 当用户在搜索框中输入文本时
        private void CustomTitleBar_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            var query = sender.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                sender.ItemsSource = null;
                return;
            }

            var results = new List<SearchResult>();
            // 搜索文件夹
            SearchFolders(query, NavView.MenuItems, results);
            // 搜索图标
            SearchIcons(query, results);

            sender.ItemsSource = results.Any() ? results : new List<SearchResult> { new SearchResult { DisplayName = resourceLoader?.GetString("SearchboxPrompt") } };
        }

        // 搜索文件夹 (递归)
        private void SearchFolders(string query, IList<object> menuItems, List<SearchResult> results)
        {
            foreach (var item in menuItems)
            {
                if (item is not NavigationViewItem nvi) continue;

                if (nvi.Content?.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                {
                    results.Add(new SearchResult
                    {
                        DisplayName = nvi.Content.ToString(),
                        Path = nvi.Tag as string,
                        Type = SearchResultType.Folder,
                        OriginalItem = nvi
                    });
                }

                if (nvi.MenuItems.Any())
                {
                    SearchFolders(query, nvi.MenuItems, results);
                }
            }
        }

        // 搜索图标
        private void SearchIcons(string query, List<SearchResult> results)
        {
            var matchingIcons = _iconsPage?.Icons
                .Where(icon => icon.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (matchingIcons == null || !matchingIcons.Any()) return;
            foreach (var icon in matchingIcons)
            {
                results.Add(new SearchResult
                {
                    DisplayName = icon.Name,
                    Path = icon.FullPath,
                    Type = SearchResultType.Icon,
                    OriginalItem = icon
                });
            }
        }

        // 当用户从建议列表中选择一项时
        private void CustomTitleBar_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SearchResult selectedResult)
            {
                NavigateToResult(selectedResult);
            }
        }

        // 当用户提交查询时 (例如按 Enter)
        private void CustomTitleBar_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is SearchResult selectedResult)
            {
                NavigateToResult(selectedResult);
            }
            else if (!string.IsNullOrEmpty(args.QueryText))
            {
                // 如果用户没有选择建议，而是直接按回车，则导航到第一个搜索结果
                if (sender.ItemsSource is List<SearchResult> results && results.Any())
                {
                    NavigateToResult(results.First());
                }
            }
        }

        // 导航到选中的结果
        private void NavigateToResult(SearchResult result)
        {
            if (result == null) return;

            switch (result.Type)
            {
                case SearchResultType.Folder:
                    if (result.Path == null) return;
                    FindAndSelectNavItem(result.Path, NavView.MenuItems);
                    break;
                case SearchResultType.Icon:
                    if (!ContentFrame.Content.Equals(_iconsPage))
                    {
                        ContentFrame.Content = _iconsPage;
                    }
                    _iconsPage?.ScrollToIcon(result.OriginalItem as IconInfo);
                    break;
            }
        }

        // 查找并选中 NavigationViewItem (递归)
        private bool FindAndSelectNavItem(string path, IList<object> items)
        {
            foreach (var obj in items)
            {
                if (obj is not NavigationViewItem nvi) continue;

                // 找到匹配项
                if (string.Equals(nvi.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                {
                    nvi.IsSelected = true;
                    return true;
                }

                // 在子项中递归查找
                if (nvi.MenuItems.Any())
                {
                    if (FindAndSelectNavItem(path, nvi.MenuItems))
                    {
                        nvi.IsExpanded = true; // 展开父项
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region 面板状态管理
        private void NavView_PaneStateChanged(NavigationView sender, object args)
        {
            UpdatePaneVisibility(sender.IsPaneOpen);
        }

        private void UpdatePaneVisibility(bool isPaneOpen)
        {
            SelectFolderButtonText.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
            DividerLine.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
            SelectFolderButton.Visibility = isPaneOpen ? Visibility.Visible : Visibility.Collapsed;
            SelectFolderButtonColum.Width = isPaneOpen ? new GridLength(4, GridUnitType.Star) : new GridLength(0, GridUnitType.Star);
        }

        private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }
        #endregion

        #region ① 选择根文件夹 —— 构建子菜单
        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            picker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;

            SelectFolderButton.IsEnabled = false;
            NavView.MenuItems.Clear();

            var loadingItem = new NavigationViewItem
            {
                Content = resourceLoader?.GetString("LoadingTip"),
                Icon = new FontIcon { Glyph = "\uE895" },
                IsEnabled = false
            };
            NavView.MenuItems.Add(loadingItem);

            try
            {
                List<FolderNavigationService.FolderNode> nodes =
                    await Task.Run(() => _folderService != null ? _folderService.BuildChildNodes(folder.Path) : new List<FolderNavigationService.FolderNode>());

                NavView.MenuItems.Clear();
                PopulateNavView(nodes, NavView.MenuItems);
            }
            catch (Exception ex)
            {
                NavView.MenuItems.Clear();
                var errorItem = new NavigationViewItem
                {
                    Content = resourceLoader?.GetString("LoadFailedTip") + ": " + ex.Message,
                    Icon = new FontIcon { Glyph = "\uE783" },
                    IsEnabled = false
                };
                NavView.MenuItems.Add(errorItem);
            }
            finally { SelectFolderButton.IsEnabled = true; }
        }
        #endregion

        #region ② 构建 NavigationView
        private void PopulateNavView(
            IEnumerable<FolderNavigationService.FolderNode> nodes, IList<object> menuItems)
        {
            foreach (var node in nodes)
            {
                var navItem = new NavigationViewItem { Content = node.Name, Tag = node.Path };
                SetNavItemIcon(navItem, node.IconPath);
                menuItems.Add(navItem);

                if (node.SubFolders.Any())
                    PopulateNavView(node.SubFolders, navItem.MenuItems);
            }
        }

        private static void SetNavItemIcon(NavigationViewItem item, string? iconPath)
        {
            var uri = !string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)
                ? new Uri(iconPath)
                : new Uri("ms-appx:///Assets/icon/default.ico");

            item.Icon = new BitmapIcon { UriSource = uri, ShowAsMonochrome = false };
        }
        #endregion

        #region ③ 左侧选中 -> 通知 IconsPage
        private void NavView_SelectionChanged(
            NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            string? path = (args.SelectedItemContainer as NavigationViewItem)?.Tag as string;
            _iconsPage?.UpdateState(path);
        }
        #endregion

        #region ④ IconsPage 改变图标 -> 局部刷新 NavigationView
        private void IconsPage_IconChanged(object? sender, IconChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.FolderPath)) return;

            if (FindNavItemByPath(e.FolderPath, NavView.MenuItems) is NavigationViewItem navItem)
            {
                SetNavItemIcon(navItem, e.IconPath);
            }
        }

        private NavigationViewItem? FindNavItemByPath(
            string path, IList<object> items)
        {
            foreach (var obj in items)
            {
                if (obj is not NavigationViewItem nvi) continue;
                if (string.Equals(nvi.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                    return nvi;

                if (nvi.MenuItems.Count > 0 &&
                    FindNavItemByPath(path, nvi.MenuItems) is NavigationViewItem found)
                    return found;
            }
            return null;
        }
        #endregion

        #region 设置窗口相关方法
        public void SetBackdrop(string backdropType)
        {
            switch (backdropType.ToLower())
            {
                case "acrylic":
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                    break;
                case "transparent":
                    SystemBackdrop = null;
                    break;
                case "mica":
                default:
                    SystemBackdrop = new MicaBackdrop();
                    break;
            }
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            if (ContentFrame.Content is SettingsPage)
            {
                // 如果当前已经是设置页，则返回主页（图标页）
                ContentFrame.Content = _iconsPage;
                NavView.IsPaneVisible = true;
            }
            else
            {
                ContentFrame.Content = _settingsPage;
                NavView.IsPaneVisible = false;
            }
        }
        #endregion
    }
}