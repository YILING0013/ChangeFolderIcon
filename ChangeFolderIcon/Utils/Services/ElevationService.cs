using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Utils.Services
{
    public static class ElevationService
    {
        /// <summary>
        /// 检查写入指定路径是否需要管理员权限。
        /// </summary>
        /// <param name="folderPath">要检查的文件夹路径。</param>
        /// <returns>如果需要提权则返回 true，否则返回 false。</returns>
        public static bool NeedsElevation(string folderPath)
        {
            try
            {
                // 检查一些已知的受保护系统文件夹
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                if (!string.IsNullOrEmpty(programFiles) && folderPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(programFilesX86) && folderPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(windows) && folderPath.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) return true;

                // 尝试创建一个临时文件来直接测试写权限
                string testFile = Path.Combine(folderPath, Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return false; // 成功写入并删除，说明有权限
            }
            catch (UnauthorizedAccessException)
            {
                return true; // 捕获到“无权限”异常，说明需要提权
            }
            catch
            {
                // 其他异常，为安全起见，也认为需要提权
                return true;
            }
        }

        /// <summary>
        /// 以管理员权限运行辅助工具。
        /// </summary>
        /// <param name="arguments">传递给 ElevatedWorker.exe 的命令行参数。</param>
        /// <returns>操作成功完成返回 true，用户取消UAC或发生错误则返回 false。</returns>
        private static Task<bool> RunWorkerAsync(string arguments)
        {
            var tcs = new TaskCompletionSource<bool>();

            // 获取 ElevatedWorker.exe 的路径，假设它和主程序在同一目录下
            string workerPath = Path.Combine(AppContext.BaseDirectory, "ElevatedWorker.exe");

            if (!File.Exists(workerPath))
            {
                // 如果找不到辅助工具，直接返回失败
                tcs.SetResult(false);
                return tcs.Task;
            }

            try
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = workerPath,
                        Arguments = arguments,
                        // Verb = "runas" 是触发 UAC 提权的关键
                        Verb = "runas",
                        UseShellExecute = true,
                        // 隐藏控制台窗口
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                process.Exited += (sender, args) =>
                {
                    // 进程退出后，根据退出代码判断是否成功
                    tcs.SetResult(process.ExitCode == 0);
                    process.Dispose();
                };

                process.Start();
            }
            catch (Exception)
            {
                // 如果用户在UAC提示框中点击“否”，会抛出异常
                tcs.SetResult(false);
            }

            return tcs.Task;
        }

        /// <summary>
        /// 提权设置文件夹图标。
        /// </summary>
        public static Task<bool> SetFolderIconElevatedAsync(string folderPath, string iconPath)
        {
            // 将路径用引号包裹，以正确处理带空格的路径
            string arguments = $"set \"{folderPath}\" \"{iconPath}\"";
            return RunWorkerAsync(arguments);
        }

        /// <summary>
        /// 提权清除文件夹图标。
        /// </summary>
        public static Task<bool> ClearFolderIconElevatedAsync(string folderPath)
        {
            string arguments = $"clear \"{folderPath}\"";
            return RunWorkerAsync(arguments);
        }
    }
}
