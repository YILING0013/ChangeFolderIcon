using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using ChangeFolderIcon.Utils.WindowsAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using WinRT.Interop;

namespace ChangeFolderIcon.UserControls.WindowControl
{
    public sealed partial class CustomTitleBar : UserControl
    {
        private Window? _parentWindow;
        private AppWindow? _appWindow;
        private bool _firstLayoutHandled = false;

        // 公开事件，以便 MainWindow 可以监听
        public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxTextChangedEventArgs>? TextChanged;
        public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxSuggestionChosenEventArgs>? SuggestionChosen;
        public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxQuerySubmittedEventArgs>? QuerySubmitted;

        public event EventHandler? SettingsClicked;

        // 公开 ItemsSource 属性
        public object ItemsSource
        {
            get => TitleBarSearchBox.ItemsSource;
            set => TitleBarSearchBox.ItemsSource = value;
        }

        public CustomTitleBar()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.LayoutUpdated += OnLayoutUpdated;

            // 将内部控件的事件连接到公共事件
            TitleBarSearchBox.TextChanged += (s, a) => TextChanged?.Invoke(s, a);
            TitleBarSearchBox.SuggestionChosen += (s, a) => SuggestionChosen?.Invoke(s, a);
            TitleBarSearchBox.QuerySubmitted += (s, a) => QuerySubmitted?.Invoke(s, a);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 尝试获取父窗口
            _parentWindow = GetParentWindow();
            if (_parentWindow == null) return;

            // 获取AppWindow
            var hWnd = WindowNative.GetWindowHandle(_parentWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // 将内容扩展到标题栏区域
                _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                _appWindow.SetIcon("Assets\\icon\\app_icon.ico");

                // 订阅窗口和控件大小变化事件，以便在需要时更新拖动区域
                _parentWindow.SizeChanged += OnParentWindowSizeChanged;
                this.SizeChanged += OnParentWindowSizeChanged;

                // 延迟一帧，确保布局完成
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, UpdateDragRegions);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 清理事件订阅，防止内存泄漏
            if (_parentWindow != null)
            {
                _parentWindow.SizeChanged -= OnParentWindowSizeChanged;
            }
            this.SizeChanged -= OnParentWindowSizeChanged;
        }

        #region UI 事件 —— 首帧布局完成后再计算拖拽区
        /// <summary>
        /// 首帧真正完成 Measure/Arrange 时触发，仅执行一次。
        /// </summary>
        private void OnLayoutUpdated(object? sender, object e)
        {
            if (_firstLayoutHandled || _appWindow is null) return;

            if (ActualWidth > 0 && ActualHeight > 0 && TitleColumn.ActualWidth > 0)
            {
                UpdateDragRegions();
                _firstLayoutHandled = true;
            }
        }

        /// <summary>
        /// 后续窗口或控件尺寸变化时更新拖动区域
        /// </summary>
        private void OnParentWindowSizeChanged(object sender, object e)
        {
            if (_appWindow?.TitleBar.ExtendsContentIntoTitleBar == true)
                UpdateDragRegions();
        }
        #endregion

        #region 拖动区域计算
        /// <summary>
        /// 计算并更新标题栏的可拖动区域
        /// </summary>
        private void UpdateDragRegions()
        {
            if (_appWindow == null || _parentWindow == null) return;
            if (this.ActualWidth == 0 || this.ActualHeight == 0) return;

            // 1. 获取DPI缩放比例
            double scale = DpiHelper.GetScaleAdjustment(_parentWindow);

            // 2. 更新为系统按钮保留的空白区域宽度
            LeftPaddingColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scale);
            RightPaddingColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scale);

            // 3. 基于列的宽度精确计算可拖动区域
            var dragRects = new List<RectInt32>();

            // 计算第一个拖动区域 (图标 + 标题 + 左侧空白区域)
            // X 坐标从系统预留区+头部之后开始
            double rect1X = LeftPaddingColumn.ActualWidth + HeaderColumn.ActualWidth;
            // 宽度是左边三个可拖动列的宽度之和
            var rect1Width = IconColumn.ActualWidth + TitleColumn.ActualWidth + LeftDragColumn.ActualWidth;

            if (rect1Width > 0)
            {
                dragRects.Add(new RectInt32(
                    (int)(rect1X * scale),
                    0,
                    (int)(rect1Width * scale),
                    (int)(AppTitleBar.ActualHeight * scale)
                ));
            }

            // 计算第二个拖动区域 (搜索框右侧的空白区域)
            // X 坐标是左边所有列（包括非拖动区域）的宽度之和
            var rect2X = rect1X + rect1Width + ContentColumn.ActualWidth;
            // 宽度是右侧拖动列的宽度
            var rect2Width = RightDragColumn.ActualWidth;

            if (rect2Width > 0)
            {
                dragRects.Add(new RectInt32(
                    (int)(rect2X * scale),
                    0,
                    (int)(rect2Width * scale),
                    (int)(AppTitleBar.ActualHeight * scale)
                ));
            }

            // 4. 设置最终的可拖动区域
            _appWindow.TitleBar.SetDragRectangles(dragRects.ToArray());
        }
        #endregion

        #region 辅助
        /// <summary>
        /// 向上遍历可视树获取顶级 Window
        /// </summary>
        /// <returns></returns>
        private Window? GetParentWindow()
        {
            // 向上遍历可视化树找到Window
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return App.window;
        }
        #endregion

        #region 设置按钮点击
        /// <summary>
        /// 处理设置按钮的点击事件。
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // 触发事件
            SettingsClicked?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}
