using ElevatedWorker.WindowsAPI;
using System;
using System.IO;

namespace ElevatedWorker
{
    class Program
    {
        // 入口点
        static void Main(string[] args)
        {
            // 设计命令行参数:
            // args[0]: 命令 ("set" 或 "clear")
            // args[1]: 目标文件夹路径
            // args[2]: 图标文件路径 (仅 "set" 命令需要)
            if (args.Length < 2)
            {
                // 参数不足，直接退出
                return;
            }

            string command = args[0].ToLower();
            string folderPath = args[1];

            try
            {
                switch (command)
                {
                    case "set":
                        if (args.Length < 3) return; // "set" 命令需要3个参数
                        string iconPath = args[2];
                        IconManager.SetFolderIcon(folderPath, iconPath);
                        break;

                    case "clear":
                        IconManager.ClearFolderIcon(folderPath);
                        break;
                }
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(Path.GetTempPath(), "ChangeFolderIcon_ElevatedWorker_Error.log");
                File.WriteAllText(logPath, $"Command: {command}\nFolder: {folderPath}\nError: {ex}");
            }
        }
    }
}
