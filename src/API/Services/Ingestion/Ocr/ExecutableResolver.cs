namespace PolicyBot.Api.Services.Ingestion.Ocr;

public static class ExecutableResolver
{
    public static string? Resolve(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        if (Path.IsPathRooted(executable) ||
            executable.Contains(Path.DirectorySeparatorChar) ||
            executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(executable) ? executable : null;
        }

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];

        foreach (var pathEntry in pathEntries)
        {
            var candidate = Path.Combine(pathEntry, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            foreach (var extension in extensions)
            {
                var candidateWithExtension = candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                    ? candidate
                    : candidate + extension;
                if (File.Exists(candidateWithExtension))
                {
                    return candidateWithExtension;
                }
            }
        }

        return null;
    }
}
