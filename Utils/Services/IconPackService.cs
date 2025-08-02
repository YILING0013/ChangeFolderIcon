using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Utils.Services
{
    public class IconPackService
    {
        private readonly string? _repoUrl;
        private readonly string? _localPath;
        public event Action<string>? StatusUpdated;

        public IconPackService(string repoUrl, string localPath)
        {
            _repoUrl = repoUrl;
            _localPath = localPath; // 直接使用传入的专用路径
        }

        public async Task<bool> UpdateIconPackAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 如果目标目录存在但不是一个有效的Git仓库，则先清理
                    if (Directory.Exists(_localPath) && !Repository.IsValid(_localPath))
                    {
                        StatusUpdated?.Invoke("Invalid repository found. Cleaning up...");
                        Directory.Delete(_localPath, true);
                    }

                    // 如果仓库目录不存在，则克隆
                    if (!Repository.IsValid(_localPath))
                    {
                        StatusUpdated?.Invoke("Cloning icon repository...");
                        Repository.Clone(_repoUrl, _localPath, new CloneOptions());
                        StatusUpdated?.Invoke("Clone successful.");
                        return true;
                    }

                    // 如果目录存在且是有效仓库，则拉取更新
                    using (var repo = new Repository(_localPath))
                    {
                        StatusUpdated?.Invoke("Fetching updates...");
                        var remote = repo.Network.Remotes["origin"];
                        var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                        Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions(), "");
                        StatusUpdated?.Invoke("Fetch complete. Merging changes...");

                        var result = repo.MergeFetchedRefs(new Signature("user", "user@example.com", DateTimeOffset.Now), new MergeOptions());

                        if (result.Status == MergeStatus.UpToDate)
                        {
                            StatusUpdated?.Invoke("Icon pack is up to date.");
                        }
                        else
                        {
                            StatusUpdated?.Invoke("Icon pack updated successfully.");
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    StatusUpdated?.Invoke($"Error updating icon pack: {ex.Message}");
                    return false;
                }
            });
        }
    }
}
