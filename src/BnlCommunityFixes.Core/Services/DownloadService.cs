namespace BnlCommunityFixes.Core.Services;

public sealed class DownloadService
{
    private readonly HttpClient httpClient;

    public DownloadService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task DownloadFileAsync(string url, string destinationPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (TryResolveLocalSource(url, out var localPath))
        {
            File.Copy(localPath, destinationPath, true);
            progress?.Report(new FileInfo(localPath).Length);
            return;
        }

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);

        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            progress?.Report(bytesRead);
        }
    }

    private static bool TryResolveLocalSource(string url, out string localPath)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            localPath = uri.LocalPath;
            return true;
        }

        if (Path.IsPathRooted(url))
        {
            localPath = url;
            return true;
        }

        localPath = string.Empty;
        return false;
    }
}
