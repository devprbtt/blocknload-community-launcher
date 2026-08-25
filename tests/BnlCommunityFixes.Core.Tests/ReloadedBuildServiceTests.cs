using BnlCommunityFixes.Core.Services;
using Xunit;

namespace BnlCommunityFixes.Core.Tests;

public sealed class ReloadedBuildServiceTests
{
    [Fact]
    public void CleanupDownloads_RemovesCompletedAndStalePartialArchives()
    {
        var root = Path.Combine(Path.GetTempPath(), "bnl-reloaded-cleanup-" + Guid.NewGuid().ToString("N"));
        var downloads = Path.Combine(root, "downloads");
        Directory.CreateDirectory(downloads);

        try
        {
            var oldArchive = Path.Combine(downloads, "old-build.zip");
            var stalePartial = Path.Combine(downloads, "old-build.zip.partial");
            var currentPartial = Path.Combine(downloads, "current-build.zip.partial");
            var unrelatedFile = Path.Combine(downloads, "keep-me.txt");
            File.WriteAllText(oldArchive, "old archive");
            File.WriteAllText(stalePartial, "stale partial");
            File.WriteAllText(currentPartial, "resumable partial");
            File.WriteAllText(unrelatedFile, "unrelated");

            using var httpClient = new HttpClient();
            var service = new ReloadedBuildService(
                new AppPaths(),
                httpClient,
                new Logger(Path.Combine(root, "launcher.log")));

            service.CleanupDownloads(downloads, currentPartial);

            Assert.False(File.Exists(oldArchive));
            Assert.False(File.Exists(stalePartial));
            Assert.True(File.Exists(currentPartial));
            Assert.True(File.Exists(unrelatedFile));

            service.CleanupDownloads(downloads, null);

            Assert.False(File.Exists(currentPartial));
            Assert.True(File.Exists(unrelatedFile));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
