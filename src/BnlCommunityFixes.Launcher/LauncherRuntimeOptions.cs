namespace BnlCommunityFixes.Launcher;

public sealed class LauncherRuntimeOptions
{
    public bool HeadlessSmokeTest { get; private set; }

    public static LauncherRuntimeOptions Parse(string[] args)
    {
        var options = new LauncherRuntimeOptions();
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--headless-smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                options.HeadlessSmokeTest = true;
            }
        }

        return options;
    }
}
