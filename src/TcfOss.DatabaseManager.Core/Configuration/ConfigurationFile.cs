using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Configuration;

public static class ConfigurationFile
{
    /// <summary>
    /// Resolves an explicit configuration path or discovers a configuration file.
    /// </summary>
    /// <param name="workingDirectory">
    /// The directory used to resolve relative paths and start configuration discovery.
    /// </param>
    /// <param name="configPath">
    /// An explicit configuration path, or <see langword="null"/> to search the working
    /// directory and its parents for a supported configuration filename.
    /// </param>
    /// <returns>
    /// The resolved configuration file, or <see langword="null"/> if discovery finds none.
    /// An explicit path is returned even when the file does not exist. # TODO Consider this behavior.
    /// </returns>
    public static FileInfo? GetConfigPath(string workingDirectory, string? configPath)
    {
        if (!string.IsNullOrEmpty(configPath))
        {
            configPath = configPath.GetAbsolutePath(workingDirectory);
            return new FileInfo(configPath);
        }

        return SearchForConfigurationPath(workingDirectory);
    }

    public static ConfigParsing.Config? LoadRawConfig(FileInfo? configPath)
    {
        if (configPath == null || !configPath.Exists)
        {
            return null;
        }
        return Serialization.ParseConfig<ConfigParsing.Config>(configPath.FullName);
    }

    /// <summary>
    /// Searches for a configuration file by walking up the directory tree from
    /// <paramref name="startDirectory"/>.
    /// </summary>
    /// <remarks>
    /// In each directory, the following filenames are considered (case-insensitive):
    /// <list type="bullet">
    ///   <item><description><c>&lt;directory-name&gt;.yaml</c> / <c>.yml</c></description></item>
    ///   <item><description><c>database-manager.yaml</c> / <c>.yml</c> (hyphen optional)</description></item>
    ///   <item><description><c>dbman.yaml</c> / <c>.yml</c></description></item>
    /// </list>
    /// The search stops at the first directory that contains at least one match.
    /// </remarks>
    /// <param name="startDirectory">The directory from which to begin the upward search.</param>
    /// <returns>
    /// The configuration file if exactly one match is found, or <see langword="null"/> if no
    /// match is found anywhere in the directory tree.
    /// </returns>
    /// <exception cref="ConfigurationException.AmbiguousConfigFile">
    /// Thrown when more than one candidate file is found in the same directory.
    /// </exception>
    private static FileInfo? SearchForConfigurationPath(string startDirectory)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(startDirectory));

        while (dir != null)
        {
            string dirNamePattern = $"{Regex.Escape(dir.Name)}|";

            var pattern = new Regex(
                $@"^({dirNamePattern}database-?manager|dbman)\.ya?ml$",
                RegexOptions.IgnoreCase);

            FileInfo[] matches = [.. dir.EnumerateFiles()
                .Where(f => pattern.IsMatch(f.Name))];

            if (matches.Length == 1)
            {
                return matches[0];
            }
            if (matches.Length > 1)
            {
                throw new ConfigurationException.AmbiguousConfigFile(dir.FullName, [.. matches.Select(f => f.Name)]);
            }

            dir = dir.Parent;
        }

        return null;
    }
}
