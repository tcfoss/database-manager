using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Hosting;

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
            Assert.IsType<CommandRunner<ConfigGeneric>>(context.CommandRunner);
            Assert.True(AppServiceProvider.GetLogger<BootstrapHelpersTests>().IsEnabled(LogLevel.Debug));
        }
        finally
        {
            AppServiceProvider.ServiceProvider = null!;
            directory.Delete(recursive: true);
        }
    }
}
