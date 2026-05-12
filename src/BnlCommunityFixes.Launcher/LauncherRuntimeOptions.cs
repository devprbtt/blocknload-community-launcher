namespace BnlCommunityFixes.Launcher;

public sealed class LauncherRuntimeOptions
{
    public bool HeadlessSmokeTest { get; private set; }
    public bool PortableMode { get; private set; }

    public static LauncherRuntimeOptions Parse(string[] args)
    {
        var options = new LauncherRuntimeOptions();
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--headless-smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                options.HeadlessSmokeTest = true;
            }
            else if (string.Equals(arg, "--portable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--no-bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                options.PortableMode = true;
            }
        }

        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "portable-launcher.flag")))
        {
            options.PortableMode = true;
        }

        return options;
    }
}
