namespace TcfOss.DatabaseManager.Core.IO;

public static class PathTools
{
    public static string GetAbsolutePath(this string path, string? basePath = null)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        basePath ??= Environment.CurrentDirectory;

        return Path.GetFullPath(Path.Combine(basePath, path));
    }
}
