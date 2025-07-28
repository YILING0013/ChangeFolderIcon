using ChangeFolderIcon.Models;
using ChangeFolderIcon.Utils.WindowsAPI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace ChangeFolderIcon.UserControls
{
    public sealed partial class IconControl : UserControl
    {
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
        #endregion

        #region 拖动逻辑

        // 外部文件夹拖到此图标上时触发
        private void Root_DragOver(object sender, DragEventArgs e)
        {
            // 检查拖动的内容是否包含文件/文件夹
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // 设置操作为“复制”
                e.AcceptedOperation = DataPackageOperation.Copy;
                // 更新拖动时的提示文字
                if (Icon != null)
                {
                    e.DragUIOverride.Caption = $"使用 '{Icon.Name}' 图标";
                    e.DragUIOverride.IsCaptionVisible = true;
                }
            }
        }

        // 外部文件夹在此图标上被放下时触发
        private async void Root_Drop(object sender, DragEventArgs e)
        {
            if (Icon == null) return;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            // 获取被拖入的项目
            var items = await e.DataView.GetStorageItemsAsync();
            // 筛选出其中的文件夹
            var folders = items.OfType<StorageFolder>().ToList();
            if (folders.Count == 0) return;

            int ok = 0, fail = 0;
            // 遍历所有被拖入的文件夹
            foreach (var f in folders)
            {
                try
                {
                    // 使用此控件自身的图标来应用
                    IconManager.SetFolderIcon(f.Path, Icon.FullPath);
                    ok++;
                }
                catch
                {
                    fail++;
                }
            }

            // 显示操作结果
            await new ContentDialog
            {
                Title = "应用结果",
                Content = $"成功应用到 {ok} 个文件夹，失败 {fail} 个。",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        #endregion
    }
}
