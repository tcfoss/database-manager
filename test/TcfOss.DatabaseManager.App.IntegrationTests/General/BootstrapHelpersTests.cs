using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MsSql.App;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.App.IntegrationTests.General;

public class BootstrapHelpersTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadExecutionContext_UsesSharedHostingAndPreservesCallbacks(bool hasConfigPath)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-bootstrap-tests-");
        try
        {
            string rootDirectory = hasConfigPath
                ? Directory.CreateDirectory(Path.Combine(directory.FullName, "configuration")).FullName
                : directory.FullName;
            var rawConfig = new GenericStartup().GetDefaultConfig(rootDirectory);
            rawConfig.ProjectDirectory = null;
            rawConfig.Logging = new LogSettings
            {
                Target = LogTarget.Console,
                LogLevel = LogLevel.Debug,
                DatabaseLogLevel = LogLevel.Error
            };
            var executionConfig = new ExecutionConfig
            {
                WorkingDirectory = directory.FullName,
                ConfigPath = hasConfigPath ? new FileInfo(Path.Combine(rootDirectory, "database-manager.yaml")) : null,
                Dialect = SqlDialect.Generic,
                RawConfig = rawConfig
            };
            using var output = new StringWriter();
            ILogger? callbackLogger = null;
            SourceManager? callbackSources = null;

            ExecutionContext context = BootstrapHelpers.LoadExecutionContext(
                executionConfig,
                relaxed: false,
                outputWriter: output,
                loggerCallback: logger => callbackLogger = logger,
                sourceManagerCallback: sources => callbackSources = sources);

            Assert.Same(output, context.Out);
            Assert.Equal(directory.FullName, context.WorkingDirectory);
            Assert.Equal(rootDirectory, context.Config.ProjectDirectory);
            Assert.Same(callbackLogger, context.Logger);
            Assert.True(context.Logger.IsEnabled(LogLevel.Debug));
            Assert.NotNull(callbackSources);
            Assert.IsType<CommandRunner<GenericConfig>>(context.CommandRunner);
            Assert.True(AppServiceProvider.GetLogger<BootstrapHelpersTests>().IsEnabled(LogLevel.Debug));
        }
        finally
        {
            AppServiceProvider.ServiceProvider = null!;
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void LoadExecutionContext_UsesCliDialectWhenItDiffersFromConfig()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-bootstrap-tests-");
        try
        {
            var logDirectory = Path.Combine(directory.FullName, "logs");
            Directory.CreateDirectory(logDirectory);
            var rawConfig = new MsConfigurationBuilder().GetDefaultConfig(directory.FullName);
            rawConfig.ProjectDirectory = null;
            rawConfig.Dialect = SqlDialect.Generic;
            rawConfig.Logging = new LogSettings
            {
                Target = LogTarget.File,
                LogDirectory = logDirectory,
                LogLevel = LogLevel.Debug,
                DatabaseLogLevel = LogLevel.Error
            };
            var executionConfig = new ExecutionConfig
            {
                WorkingDirectory = directory.FullName,
                CliDialect = SqlDialect.MsSql,
                ConfigDialect = SqlDialect.Generic,
                Dialect = SqlDialect.MsSql,
                RawConfig = rawConfig
            };

            ExecutionContext context = BootstrapHelpers.LoadExecutionContext(
                executionConfig,
                relaxed: false,
                outputWriter: new StringWriter(),
                loggerCallback: _ => { },
                sourceManagerCallback: _ => { });

            Assert.IsType<MsCommandRunner>(context.CommandRunner);

            string logFile = Directory.GetFiles(logDirectory).First(f => f.EndsWith(".json"));
            Assert.True(File.Exists(logFile));

            string logContent = File.ReadAllText(logFile);
            Assert.Contains("Dialect mismatch", logContent);
        }
        finally
        {
            AppServiceProvider.ServiceProvider = null!;
            directory.Delete(recursive: true);
        }
    }
}
