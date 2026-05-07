namespace BnlCommunityFixes.Updater;

public static class UpdaterArgumentParser
{
    public static UpdaterArguments Parse(string[] args)
    {
        var parsed = new UpdaterArguments();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--target":
                    parsed.TargetPath = ReadValue(args, ref i, arg);
                    break;
                case "--source":
                    parsed.SourcePath = ReadValue(args, ref i, arg);
                    break;
                case "--updater-target":
                    parsed.UpdaterTargetPath = ReadValue(args, ref i, arg);
                    break;
                case "--updater-source":
                    parsed.UpdaterSourcePath = ReadValue(args, ref i, arg);
                    break;
                case "--external-target":
                    parsed.ExternalTargetPath = ReadValue(args, ref i, arg);
                    break;
                case "--pid":
                    parsed.ProcessId = int.Parse(ReadValue(args, ref i, arg));
                    break;
                case "--log":
                    parsed.LogPath = ReadValue(args, ref i, arg);
                    break;
                case "--restart-arg":
                    parsed.RestartArguments.Add(ReadValue(args, ref i, arg));
                    break;
                case "--restart":
                    parsed.Restart = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown updater argument: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(parsed.TargetPath) ||
            string.IsNullOrWhiteSpace(parsed.SourcePath) ||
            parsed.ProcessId <= 0 ||
            string.IsNullOrWhiteSpace(parsed.LogPath))
        {
            throw new InvalidOperationException("Missing required updater arguments.");
        }

        return parsed;
    }

    private static string ReadValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for argument {name}");
        }

        index++;
        return args[index];
    }
}
