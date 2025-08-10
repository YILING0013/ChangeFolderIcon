using ChangeFolderIcon.Models;
using ChangeFolderIcon.Utils.WindowsAPI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace ChangeFolderIcon.UserControls
{
    public sealed partial class IconControl : UserControl
    {
        private bool _isDragOver = false;
        private ResourceLoader resourceLoader = new();

        public IconControl() => InitializeComponent();

        #region 依赖属性
        public IconInfo Icon
        {
            get => (IconInfo)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(IconInfo),
                typeof(IconControl), new PropertyMetadata(null));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool),
                typeof(IconControl), new PropertyMetadata(false, OnIsSelectedChanged));

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (IconControl)d;
            control.SelectionIndicator.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region 鼠标悬停动画
        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragOver)
            {
                HoverStoryboard.Begin();
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragOver)
            {
                UnhoverStoryboard.Begin();
            }
        }
        #endregion

        #region 拖放逻辑

        // 外部文件夹拖到此图标上时触发
        private void Root_DragOver(object sender, DragEventArgs e)
        {
            // 检查拖动的内容是否包含文件/文件夹
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // 设置操作为"复制"
                e.AcceptedOperation = DataPackageOperation.Copy;

                // 显示拖放覆盖层
                if (!_isDragOver)
                {
                    _isDragOver = true;
                    DragOverlay.Visibility = Visibility.Visible;
                    HoverStoryboard.Begin();
                }

                // 更新拖动时的提示文字
                if (Icon != null)
                {
                    e.DragUIOverride.Caption = resourceLoader.GetString("use") + Icon.Name + resourceLoader.GetString("icon");
                    e.DragUIOverride.IsCaptionVisible = true;
                    e.DragUIOverride.IsGlyphVisible = true;
                    e.DragUIOverride.IsContentVisible = true;
                }
            }
        }

        private void Root_DragLeave(object sender, DragEventArgs e)
        {
            // 隐藏拖放覆盖层
            if (_isDragOver)
            {
                _isDragOver = false;
                DragOverlay.Visibility = Visibility.Collapsed;
                UnhoverStoryboard.Begin();
            }
        }

        // 外部文件夹在此图标上被放下时触发
        private async void Root_Drop(object sender, DragEventArgs e)
        {
            // 隐藏拖放覆盖层
            _isDragOver = false;
            DragOverlay.Visibility = Visibility.Collapsed;
            UnhoverStoryboard.Begin();

            if (Icon == null) return;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            // 获取被拖入的项目
            var items = await e.DataView.GetStorageItemsAsync();
            // 筛选出其中的文件夹
            var folders = items.OfType<StorageFolder>().ToList();
            if (folders.Count == 0) return;

            // 创建进度对话框
            var progressDialog = new ContentDialog
            {
                Title = resourceLoader.GetString("ApplyingIcons"),
                Content = new ProgressRing { IsActive = true },
                XamlRoot = this.XamlRoot,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false
            };

            // 显示进度对话框
            var dialogTask = progressDialog.ShowAsync();

            // 在 UI 线程读取图标路径
            string iconPath = Icon.FullPath;

            // 使用非 UI 线程运行图标设置逻辑
            var (ok, fail) = await Task.Run(() =>
            {
                int ok = 0, fail = 0;

                foreach (var folder in folders)
                {
                    try
                    {
                        // 使用此控件自身的图标来应用
                        IconManager.SetFolderIcon(folder.Path, iconPath);
                        ok++;
                    }
                    catch
                    {
                        fail++;
                    }
                }
                return (ok, fail);
            });

            // 关闭进度对话框
            progressDialog.Hide();

            // 显示操作结果
            var resultDialog = new ContentDialog
            {
                Title = resourceLoader.GetString("Result"),
                CloseButtonText = resourceLoader.GetString("CloseButtonText"),
                XamlRoot = this.XamlRoot
            };

            if (fail == 0)
            {
                resultDialog.Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new SymbolIcon { Symbol = Symbol.Accept },
                        new TextBlock
                        {
                            Text = resourceLoader.GetString("SuccessfullyAppliedTip_1") + ok + " " + resourceLoader.GetString("SuccessfullyAppliedTip_2"),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                };
            }
            else
            {
                resultDialog.Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new SymbolIcon { Symbol = Symbol.Important },
                        new TextBlock
                        {
                            Text = resourceLoader.GetString("Success") + ok + "\n" + resourceLoader.GetString("Failed") + fail,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                };
            }

            await resultDialog.ShowAsync();
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);
            Root_DragLeave(this, e);
        }

        #endregion
    }
}