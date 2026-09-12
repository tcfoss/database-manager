using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public abstract class StartupBase
{
    protected const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Resolves the root directory and configuration file path from optional inputs.
    /// </summary>
    /// <param name="workingDirectory">
    /// The working directory for the command, or <see langword="null"/> to use the current
    /// directory (or, if <paramref name="configPath"/> is provided, the directory containing
    /// the configuration file).
    /// </param>
    /// <param name="configPath">
    /// An explicit path to the configuration file, or <see langword="null"/> to default to
    /// <c>database-manager.yaml</c> in the resolved root directory.
    /// </param>
    /// <returns>
    /// A tuple of fully-qualified absolute paths: the resolved root directory and the
    /// resolved configuration file path.
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
            string dirNamePattern = string.IsNullOrEmpty(dir.Name)
                ? ""
                : $"{Regex.Escape(dir.Name)}|";

            var pattern = new Regex(
                $@"^({dirNamePattern}database-?manager|dbman)\.ya?ml$",
                RegexOptions.IgnoreCase);

            string[] matches = [.. dir.EnumerateFiles()
                .Where(f => pattern.IsMatch(f.Name))
                .Select(f => f.FullName)];

            if (matches.Length == 1)
            {
                return new FileInfo(matches[0]);
            }
            if (matches.Length > 1)
            {
                throw new ConfigurationException.AmbiguousConfigFile(dir.FullName, matches);
            }

            dir = dir.Parent;
        }

        return null;
    }

    public abstract ConfigParsing.Config GetDefaultConfig(string rootDirectory);

    /// <summary>
    /// Starts the application with the provided configuration.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the database project.</param>
    /// <param name="rawConfig">The parsed raw configuration.</param>
    /// <param name="environmentVariableReader">An instance to read environment variables.</param>
    /// <param name="builder">The host builder to configure services and logging.</param>
    /// <param name="loggingSetupFunc">
    /// A function to set up logging services in the host builder. The provided arguments are
    /// the host builder and the log settings from the configuration. The function should return
    /// a tuple containing the logger instance and the log file path (if applicable).
    /// </param>
    /// <param name="logSetupAction">
    /// An optional action to perform after the logger has been set up. The provided arguments
    /// are a logger instance and the log file path (if applicable).
    /// </param>
    /// <param name="configSetupAction"></param>
    /// An optional action to perform after the full dialect-specific configuration has been
    /// built. The provided argument is the built configuration.
    /// <param name="relaxed">
    /// Whether to use relaxed configuration parsing (omitting all definition-related checks
    /// and some dialect-specific validations).
    /// </param>
    public abstract void Configure(
        string rootDirectory,
        ConfigParsing.Config rawConfig,
        IHostApplicationBuilder builder,
        IReadEnvironmentVariables environmentVariableReader,
        Func<IHostApplicationBuilder, LogSettings, (ILogger, string?)> loggingSetupFunc,
        Action<ILogger, string?>? logSetupAction = null,
        Action<ConfigBase>? configSetupAction = null,
        bool relaxed = false
    );

    public abstract void ConfigureAppServiceProvider(IServiceProvider serviceProvider);
}
