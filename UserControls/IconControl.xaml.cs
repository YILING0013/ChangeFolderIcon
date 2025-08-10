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

        #region ��������
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

        #region �����ͣ����
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

        #region �Ϸ��߼�

        // �ⲿ�ļ����ϵ���ͼ����ʱ����
        private void Root_DragOver(object sender, DragEventArgs e)
        {
            // ����϶��������Ƿ�����ļ�/�ļ���
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // ���ò���Ϊ"����"
                e.AcceptedOperation = DataPackageOperation.Copy;

                // ��ʾ�ϷŸ��ǲ�
                if (!_isDragOver)
                {
                    _isDragOver = true;
                    DragOverlay.Visibility = Visibility.Visible;
                    HoverStoryboard.Begin();
                }

                // �����϶�ʱ����ʾ����
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
            // �����ϷŸ��ǲ�
            if (_isDragOver)
            {
                _isDragOver = false;
                DragOverlay.Visibility = Visibility.Collapsed;
                UnhoverStoryboard.Begin();
            }
        }

        // �ⲿ�ļ����ڴ�ͼ���ϱ�����ʱ����
        private async void Root_Drop(object sender, DragEventArgs e)
        {
            // �����ϷŸ��ǲ�
            _isDragOver = false;
            DragOverlay.Visibility = Visibility.Collapsed;
            UnhoverStoryboard.Begin();

            if (Icon == null) return;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            // ��ȡ���������Ŀ
            var items = await e.DataView.GetStorageItemsAsync();
            // ɸѡ�����е��ļ���
            var folders = items.OfType<StorageFolder>().ToList();
            if (folders.Count == 0) return;

            // �������ȶԻ���
            var progressDialog = new ContentDialog
            {
                Title = resourceLoader.GetString("ApplyingIcons"),
                Content = new ProgressRing { IsActive = true },
                XamlRoot = this.XamlRoot,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false
            };

            // ��ʾ���ȶԻ���
            var dialogTask = progressDialog.ShowAsync();

            // �� UI �̶߳�ȡͼ��·��
            string iconPath = Icon.FullPath;

            // ʹ�÷� UI �߳�����ͼ�������߼�
            var (ok, fail) = await Task.Run(() =>
            {
                int ok = 0, fail = 0;

                foreach (var folder in folders)
                {
                    try
                    {
                        // ʹ�ô˿ؼ������ͼ����Ӧ��
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

            // �رս��ȶԻ���
            progressDialog.Hide();

            // ��ʾ�������
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