using BnlCommunityFixes.Core.Models;

namespace BnlCommunityFixes.Core.Services;

public sealed class PatchService
{
    public static byte[] RevertPatchedContent(byte[] content, PatchFileDefinition fileDefinition)
    {
        var reverted = content;
        foreach (var patch in OrderByDescendingOffset(fileDefinition.Patches))
        {
            reverted = ApplyBinaryPatch(
                reverted,
                ParseOffset(patch.Offset),
                ParseHex(patch.After),
                ParseHex(patch.Before));
        }

        return reverted;
    }

    public void ApplyPatchSet(string basePath, LauncherConfig config, string patchId, Logger logger, IReadOnlyCollection<string>? skipPaths = null)
    {
        if (!config.PatchConfigurations.TryGetValue(patchId, out var patchDefinition))
        {
            throw new InvalidOperationException($"Patch config '{patchId}' not found.");
        }

        logger.Info($"Applying patch configuration: {patchDefinition.Name}");

        foreach (var fileEntry in patchDefinition.Files)
        {
            var relativePath = fileEntry.Key;
            if (skipPaths is not null && skipPaths.Contains(relativePath))
            {
                logger.Info($"Skipping patch target: {relativePath}");
                continue;
            }

            ApplyFilePatch(basePath, relativePath, fileEntry.Value, logger);
        }
    }

    public void RevertPatchSet(string basePath, LauncherConfig config, string patchId, Logger logger)
    {
        if (!config.PatchConfigurations.TryGetValue(patchId, out var patchDefinition))
        {
            throw new InvalidOperationException($"Patch config '{patchId}' not found.");
        }

        logger.Info($"Reverting patches for: {patchDefinition.Name}");
        var revertedCount = 0;

        foreach (var fileEntry in patchDefinition.Files)
        {
            if (RevertFilePatch(basePath, fileEntry.Key, fileEntry.Value, logger))
            {
                revertedCount++;
            }
        }

        logger.Info($"Revert complete. Processed {revertedCount} file(s).");
    }

    private static void ApplyFilePatch(string basePath, string relativePath, PatchFileDefinition fileDefinition, Logger logger)
    {
        var fullPath = Path.Combine(basePath, relativePath);

        if (string.IsNullOrWhiteSpace(fileDefinition.HashBefore))
        {
            logger.Info($"Checking creation: {relativePath}");
            if (File.Exists(fullPath))
            {
                var currentHash = HashService.ComputeSha1(fullPath);
                if (HashesEqual(currentHash, fileDefinition.HashAfter))
                {
                    logger.Info("  -> Already created.");
                    return;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var createdBytes = ParseHex(fileDefinition.Patches.FirstOrDefault()?.After ?? string.Empty);
            File.WriteAllBytes(fullPath, createdBytes);
            logger.Info("  -> File created.");
            return;
        }

        if (string.IsNullOrWhiteSpace(fileDefinition.HashAfter))
        {
            logger.Info($"Checking deletion: {relativePath}");
            if (!File.Exists(fullPath))
            {
                logger.Info("  -> Already deleted.");
                return;
            }

            var currentHash = HashService.ComputeSha1(fullPath);
            if (HashesEqual(currentHash, fileDefinition.HashBefore))
            {
                File.Delete(fullPath);
                logger.Info("  -> File deleted.");
                return;
            }

            logger.Warning($"  -> Skipping deletion of {relativePath} (Hash mismatch/Modified).");
            return;
        }

        logger.Info($"Checking: {relativePath}");
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Missing file: {fullPath}");
        }

        var fileHash = HashService.ComputeSha1(fullPath);
        if (HashesEqual(fileHash, fileDefinition.HashAfter))
        {
            logger.Info("  -> Already patched.");
            return;
        }

        if (!HashesEqual(fileHash, fileDefinition.HashBefore))
        {
            throw new InvalidOperationException(
                $"Could not recognize the installed game version for '{relativePath}'. Please verify the game files via Steam and try again. Found hash: {fileHash}");
        }

        var content = File.ReadAllBytes(fullPath);
        foreach (var patch in OrderByDescendingOffset(fileDefinition.Patches))
        {
            logger.Info($"  -> Applying: {patch.Description}");
            content = ApplyBinaryPatch(
                content,
                ParseOffset(patch.Offset),
                ParseHex(patch.Before),
                ParseHex(patch.After));
        }

        File.WriteAllBytes(fullPath, content);
        logger.Info("  -> Success.");
    }

    private static bool RevertFilePatch(string basePath, string relativePath, PatchFileDefinition fileDefinition, Logger logger)
    {
        var fullPath = Path.Combine(basePath, relativePath);

        if (string.IsNullOrWhiteSpace(fileDefinition.HashBefore))
        {
            if (!File.Exists(fullPath))
            {
                return false;
            }

            var currentHash = HashService.ComputeSha1(fullPath);
            if (!HashesEqual(currentHash, fileDefinition.HashAfter))
            {
                logger.Warning($"  -> Skipping removal of {relativePath} (Modified).");
                return false;
            }

            File.Delete(fullPath);
            logger.Info($"  -> Removed created file: {relativePath}");
            return true;
        }

        if (string.IsNullOrWhiteSpace(fileDefinition.HashAfter))
        {
            if (File.Exists(fullPath))
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var restoredBytes = ParseHex(fileDefinition.Patches.FirstOrDefault()?.Before ?? string.Empty);
            File.WriteAllBytes(fullPath, restoredBytes);
            logger.Info($"  -> Restored deleted file: {relativePath}");
            return true;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        var fileHash = HashService.ComputeSha1(fullPath);
        if (HashesEqual(fileHash, fileDefinition.HashBefore))
        {
            logger.Info($"  -> {relativePath} is already clean.");
            return false;
        }

        if (!HashesEqual(fileHash, fileDefinition.HashAfter))
        {
            logger.Warning($"  -> Skipping {relativePath} (Modified by unknown source).");
            return false;
        }

        logger.Info($"  -> Restoring original bytes for {relativePath}...");
        var content = File.ReadAllBytes(fullPath);
        content = RevertPatchedContent(content, fileDefinition);

        File.WriteAllBytes(fullPath, content);
        return true;
    }

    private static byte[] ApplyBinaryPatch(byte[] content, long offset, byte[] oldBytes, byte[] newBytes)
    {
        if (offset < 0 || offset > content.LongLength)
        {
            throw new InvalidOperationException($"Patch offset {offset} is outside the file bounds.");
        }

        if (offset + oldBytes.LongLength > content.LongLength)
        {
            throw new InvalidOperationException("Patch expected bytes exceed file bounds.");
        }

        for (var i = 0; i < oldBytes.Length; i++)
        {
            if (content[offset + i] != oldBytes[i])
            {
                throw new InvalidOperationException($"Patch validation failed at offset 0x{offset:X}.");
            }
        }

        var newContent = new byte[content.Length - oldBytes.Length + newBytes.Length];
        Buffer.BlockCopy(content, 0, newContent, 0, (int)offset);
        Buffer.BlockCopy(newBytes, 0, newContent, (int)offset, newBytes.Length);

        var sourceTailOffset = (int)(offset + oldBytes.Length);
        var destinationTailOffset = (int)(offset + newBytes.Length);
        var tailLength = content.Length - sourceTailOffset;
        Buffer.BlockCopy(content, sourceTailOffset, newContent, destinationTailOffset, tailLength);

        return newContent;
    }

    private static IEnumerable<BinaryPatchDefinition> OrderByDescendingOffset(IEnumerable<BinaryPatchDefinition> patches)
    {
        return patches.OrderByDescending(static patch => ParseOffset(patch.Offset));
    }

    private static long ParseOffset(string value)
    {
        return Convert.ToInt64(value, 16);
    }

    private static byte[] ParseHex(string value)
    {
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        if (normalized.Length % 2 != 0)
        {
            throw new InvalidOperationException($"Invalid hex payload length: '{value}'.");
        }

        var bytes = new byte[normalized.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(normalized.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private static bool HashesEqual(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
