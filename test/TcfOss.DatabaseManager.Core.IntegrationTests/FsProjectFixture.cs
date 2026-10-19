using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.IntegrationTests;

public partial class FsProjectFixture(string? prefix = null) : IDisposable
{
    public DirectoryInfo RootDirectory { get; } = Directory.CreateTempSubdirectory(prefix ?? "dbman-test_");

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

    public async Task UpdateConfigurationAsync(
        SqlDialect? dialect = null,
        string? hostName = null,
        string? userName = null,
        ushort? port = null,
        string? password = null,
        CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(CombinePath("database-manager.yaml"), ct);

        if (dialect.HasValue)
        {
            text = GetDialectRegex().Replace(text, $"Dialect: {dialect.Value}");
        }
        if (hostName is not null)
        {
            text = GetHostnameRegex().Replace(text, $"HostName: {hostName}");
        }
        if (userName is not null)
        {
            text = GetUsernameRegex().Replace(text, $"UserName: {userName}");
        }
        if (port.HasValue)
        {
            text = GetPortRegex().Replace(text, $"Port: {port.Value}");
        }
        if (password is not null)
        {
            text = GetPasswordRegex().Replace(text, $"Password: {password}");
        }
        await File.WriteAllTextAsync(CombinePath("database-manager.yaml"), text, ct);
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

    [GeneratedRegex(@"HostName: \w+")]
    private static partial Regex GetHostnameRegex();

    [GeneratedRegex(@"Dialect: \w+")]
    private static partial Regex GetDialectRegex();
    [GeneratedRegex(@"UserName: \w+")]
    private static partial Regex GetUsernameRegex();
    [GeneratedRegex(@"Port: \d+")]
    private static partial Regex GetPortRegex();
    [GeneratedRegex(@"Password: \w+")]
    private static partial Regex GetPasswordRegex();
}
