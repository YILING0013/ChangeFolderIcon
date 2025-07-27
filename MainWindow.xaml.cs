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
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using static ChangeFolderIcon.Utils.Services.FolderNavigationService;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ChangeFolderIcon
{
    public sealed partial class MainWindow : Window
    {
        private readonly FolderNavigationService _folderService = new FolderNavigationService();

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// “选择文件夹”按钮的点击事件处理。
        /// </summary>
        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                SelectFolderButton.IsEnabled = false;
                NavView.MenuItems.Clear();
                var loadingItem = new NavigationViewItem { Content = "加载中...", IsEnabled = false };
                NavView.MenuItems.Add(loadingItem);

                try
                {
                    // 1. 在后台线程上调用服务来构建数据模型树
                    List<FolderNode> childNodes = await Task.Run(() => _folderService.BuildChildNodes(folder.Path));

                    // 2. 返回UI线程，清空加载指示器
                    NavView.MenuItems.Clear();

                    // 3. 根据构建好的数据模型，在UI线程上填充NavigationView
                    PopulateNavView(childNodes, NavView.MenuItems);
                }
                catch (Exception ex)
                {
                    NavView.MenuItems.Clear();
                    var errorItem = new NavigationViewItem { Content = $"加载失败: {ex.Message}", IsEnabled = false };
                    NavView.MenuItems.Add(errorItem);
                }
                finally
                {
                    SelectFolderButton.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// 使用预先构建的节点树来递归填充 NavigationView。
        /// </summary>
        /// <param name="nodes">要添加到UI的文件夹数据节点列表</param>
        /// <param name="menuItems">用于添加新创建的 NavigationViewItem 的UI集合</param>
        private void PopulateNavView(List<FolderNode> nodes, IList<object> menuItems)
        {
            foreach (var node in nodes)
            {
                var navItem = new NavigationViewItem
                {
                    Content = node.Name,
                    Tag = node.Path,
                };

                // 检查是否存在自定义图标路径
                if (!string.IsNullOrEmpty(node.IconPath) && File.Exists(node.IconPath))
                {
                    try
                    {
                        // 如果存在，使用 BitmapIcon 显示自定义图标
                        navItem.Icon = new BitmapIcon
                        {
                            UriSource = new Uri(node.IconPath),
                            ShowAsMonochrome = false // 确保显示彩色图标
                        };
                    }
                    catch (Exception)
                    {
                        // 如果创建图标失败（例如路径无效）
                        navItem.Icon = new BitmapIcon
                        {
                            UriSource = new Uri("ms-appx:///Assets/icon/default.ico"),
                            ShowAsMonochrome = false
                        };
                    }
                }
                else
                {
                    // 否则，使用默认的文件夹图标
                    navItem.Icon = new BitmapIcon
                    {
                        UriSource = new Uri("ms-appx:///Assets/icon/default.ico"),
                        ShowAsMonochrome = false
                    };
                }

                menuItems.Add(navItem);

                // 递归为其子文件夹填充菜单项
                if (node.SubFolders.Any())
                {
                    PopulateNavView(node.SubFolders, navItem.MenuItems);
                }
            }
        }
    }
}