using BnlCommunityFixes.Core.Services;

namespace BnlCommunityFixes.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        UpdaterArguments parsed;
        try
        {
            parsed = UpdaterArgumentParser.Parse(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }

        var logger = new Logger(parsed.LogPath);
        logger.Info("Updater starting.");

        var installer = new UpdateInstaller(logger);
        return await installer.RunAsync(parsed);
    }
}
