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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ChangeFolderIcon.UserControls
{
    public sealed partial class IconControl : UserControl
    {
        public IconControl()
        {
            this.InitializeComponent();
        }

        public IconInfo Icon
        {
            get => (IconInfo)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(IconInfo), typeof(IconControl), new PropertyMetadata(null));

        private void Root_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "将文件夹拖到此图标以应用";
            }
        }

        private async void Root_Drop(object sender, DragEventArgs e)
        {
            if (Icon == null) return;

            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            var folders = items.OfType<StorageFolder>().ToList();

            if (folders.Count == 0) return;

            int ok = 0, fail = 0;
            foreach (var f in folders)
            {
                try
                {
                    IconManager.SetFolderIcon(f.Path, Icon.FullPath);
                    ok++;
                }
                catch { fail++; }
            }

            // 简单反馈（可换成 InAppNotification/TeachingTip）
            var dlg = new ContentDialog
            {
                Title = "应用结果",
                Content = $"成功：{ok}，失败：{fail}",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dlg.ShowAsync();
        }
    }
}
