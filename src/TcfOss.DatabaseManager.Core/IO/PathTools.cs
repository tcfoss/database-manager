namespace TcfOss.DatabaseManager.Core.IO;

public static class PathTools
{
    public static string GetAbsolutePath(this string path, string basePathDefault)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(basePathDefault, path));
    }
}
