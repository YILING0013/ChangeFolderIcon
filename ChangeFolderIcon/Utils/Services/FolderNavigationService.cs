using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Utils.Services
{
    /// <summary>
    /// 提供加载文件夹结构相关的功能。
    /// </summary>
    public class FolderNavigationService
    {
        /// <summary>
        /// 递归地使用 System.IO 构建子文件夹的数据模型树。
        /// </summary>
        /// <param name="path">父文件夹的路径。</param>
        /// <returns>子文件夹节点列表。</returns>
        public List<FolderNode> BuildChildNodes(string path)
        {
            var children = new List<FolderNode>();
            try
            {
                foreach (var dirPath in Directory.EnumerateDirectories(path))
                {
                    var childNode = new FolderNode
                    {
                        Name = Path.GetFileName(dirPath),
                        Path = dirPath,
                        // 从 desktop.ini 获取图标路径
                        IconPath = GetIconPathFromDesktopIni(dirPath),
                        // 递归为子文件夹构建它们的子节点
                        SubFolders = BuildChildNodes(dirPath)
                    };
                    children.Add(childNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 忽略无权访问的文件夹，避免程序崩溃
            }
            catch (IOException)
            {
                // 忽略其他可能的IO错误
            }
            return children;
        }

        /// <summary>
        /// 尝试从文件夹的 desktop.ini 文件中读取图标路径。
        /// </summary>
        /// <param name="folderPath">文件夹的完整路径。</param>
        /// <returns>图标文件的路径，如果不存在或解析失败则返回 null。</returns>
        private string? GetIconPathFromDesktopIni(string folderPath)
        {
            var iniPath = Path.Combine(folderPath, "desktop.ini");
            if (!File.Exists(iniPath)) return null;

            try
            {
                var lines = File.ReadAllLines(iniPath);
                var iconResourceLine = lines.FirstOrDefault(line => line.Trim().StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase));

                if (iconResourceLine != null)
                {
                    // 提取等号后面的路径部分
                    var pathPart = iconResourceLine.Split('=')[1].Trim();

                    // 移除图标索引 (例如 ",0")
                    int commaIndex = pathPart.LastIndexOf(',');
                    if (commaIndex != -1)
                    {
                        if (int.TryParse(pathPart.Substring(commaIndex + 1), out _))
                        {
                            pathPart = pathPart.Substring(0, commaIndex);
                        }
                    }

                    // 展开路径中的环境变量 (例如 %SystemRoot%)
                    var expandedPath = Environment.ExpandEnvironmentVariables(pathPart);

                    // 如果路径不是绝对路径，则视为相对于当前文件夹
                    if (!Path.IsPathRooted(expandedPath))
                    {
                        expandedPath = Path.GetFullPath(Path.Combine(folderPath, expandedPath));
                    }

                    // 只有当路径指向一个实际存在的文件时才返回它
                    if (File.Exists(expandedPath))
                    {
                        return expandedPath;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略解析过程中的任何错误，并返回null
            }

            return null;
        }


        public class FolderNode
        {
            public string? Name { get; set; }
            public string? Path { get; set; }
            /// <summary>
            /// 文件夹自定义图标的路径 (如果有)。
            /// </summary>
            public string? IconPath { get; set; }
            public List<FolderNode> SubFolders { get; set; } = new List<FolderNode>();
        }
    }
}
