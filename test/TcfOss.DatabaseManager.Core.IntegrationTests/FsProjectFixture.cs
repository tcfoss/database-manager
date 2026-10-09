namespace TcfOss.DatabaseManager.Core.IntegrationTests;

public class FsProjectFixture : IDisposable
{
    public DirectoryInfo RootDirectory { get; }

    public FsProjectFixture()
    {
        RootDirectory = new DirectoryInfo(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        if (RootDirectory.Exists)
        {
            throw new InvalidOperationException($"Root directory {RootDirectory.FullName} already exists.");
        }
        RootDirectory.Create();
    }

    public void CopyFiles(string sourceDirectory, string? targetDirectory = null)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory '{sourceDirectory}' does not exist.");
        }

        targetDirectory ??= RootDirectory.FullName;

        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destFile = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDirectory))
        {
            var destDir = Path.Combine(targetDirectory, Path.GetFileName(dir));
            CopyFiles(dir, destDir);
        }
    }

    public void CopyFile(string sourceFile, string? targetDirectory = null)
    {
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Source file '{sourceFile}' does not exist.");
        }

        targetDirectory ??= RootDirectory.FullName;

        var destFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
        File.Copy(sourceFile, destFile, true);
    }

    public async Task WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var targetPath = CombinePath(relativePath);
        var targetDirectory = Path.GetDirectoryName(targetPath)!;
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }
        await File.WriteAllTextAsync(targetPath, content, cancellationToken);
    }

    /// <summary>
    /// Adds the given path segments to the root directory of the fixture.
    /// </summary>
    /// <param name="paths"></param>
    /// <returns>
    ///     The absolute path, within the temporary directory, of the file
    ///     specified by the given path segments.
    /// </returns>
    public string CombinePath(params string[] paths)
    {
        var combinedPath = Path.Combine(paths);
        var targetPath = Path.Combine(RootDirectory.FullName, combinedPath);
        return targetPath;
    }

    public void Dispose()
    {
        if (RootDirectory.Exists)
        {
            RootDirectory.Delete(true);
        }
        GC.SuppressFinalize(this);
    }
}
