using System;

namespace ChangeFolderIcon.Utils.Events
{
    /// <summary>
    /// IconsPage 在图标被应用 / 清除后抛出的事件参数。
    /// IconPath == null 表示已清除图标。
    /// </summary>
    public sealed class IconChangedEventArgs : EventArgs
    {
        public string FolderPath { get; }
        public string? IconPath { get; }

        public IconChangedEventArgs(string folderPath, string? iconPath)
        {
            FolderPath = folderPath;
            IconPath = iconPath;
        }
    }
}
