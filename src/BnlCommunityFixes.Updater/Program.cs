using BnlCommunityFixes.Core.Services;
using BnlCommunityFixes.Core.Updating;

namespace BnlCommunityFixes.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        BnlCommunityFixes.Core.Updating.UpdaterArguments parsed;
        try
        {
            parsed = BnlCommunityFixes.Core.Updating.UpdaterArgumentParser.Parse(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }

        var logger = new Logger(parsed.LogPath);
        logger.Info("Updater starting.");

        var installer = new BnlCommunityFixes.Core.Updating.UpdateInstaller(logger);
        return await installer.RunAsync(parsed);
    }
}
