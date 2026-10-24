using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MsSql.App;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.App.IntegrationTests.General;

public class BootstrapHelpersTests
{
    [Theory]
    [InlineData("--help", 0)]
    [InlineData("--not-an-option", 1)]
    public void Run_HandlesHelpAndArgumentErrorsWithoutCreatingHost(string argument, int expectedExitCode)
    {
        AppServiceProvider.ServiceProvider = null!;
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new CommandLineInterface { Out = output, Error = error };

        int result = cli.Run([argument]);

        Assert.Equal(expectedExitCode, result);
        Assert.Contains("parse-files", output.ToString());
        Assert.Equal(expectedExitCode != 0, error.ToString().Length > 0);
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.GetLogger<BootstrapHelpersTests>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_DisposesHostAfterExecutionAndErrorReporting(bool commandFails)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-cli-lifetime-tests-");
        try
        {
            string logDirectory = Path.Combine(directory.FullName, "logs");
            File.WriteAllText(Path.Combine(directory.FullName, "database-manager.yaml"), $"""
                Dialect: Generic
                Logging:
                  Target: File
                  LogDirectory: {logDirectory}
                """);
            File.WriteAllText(Path.Combine(directory.FullName, "input.sql"), "SELECT 1;");
            using var output = new StringWriter();
            using var error = new ServiceCheckingWriter();
            var cli = new CommandLineInterface { Out = output, Error = error };

            int result = cli.Run(["parse-files", commandFails ? "missing.sql" : "input.sql", "--working-dir", directory.FullName]);

            Assert.Equal(commandFails ? 1 : 0, result);
            Assert.Throws<ObjectDisposedException>(() => AppServiceProvider.GetLogger<BootstrapHelpersTests>());
            if (commandFails)
            {
                Assert.Contains("File not found", error.ToString());
                string logFile = Assert.Single(Directory.GetFiles(logDirectory, "*.json"));
                Assert.Contains("File not found exception.", File.ReadAllText(logFile));
            }
            else
            {
                Assert.True(File.Exists(Path.Combine(directory.FullName, "input.parsed.json")));
            }

            Assert.Equal(result, cli.Run(["parse-files", commandFails ? "missing.sql" : "input.sql", "--working-dir", directory.FullName]));
            Assert.Throws<ObjectDisposedException>(() => AppServiceProvider.GetLogger<BootstrapHelpersTests>());
        }
        finally
        {
            AppServiceProvider.ServiceProvider = null!;
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_DisposesHostAfterContextValidationFails()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-cli-lifetime-tests-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "database-manager.yaml"), """
                Dialect: MsSql
                Catalog: master
                Credentials:
                  Hostname: 127.0.0.1
                  Port: '1'
                  ConnectionTimeout: 1
                Logging:
                  Target: Console
                """);
            using var output = new StringWriter();
            using var error = new ServiceCheckingWriter();
            var cli = new CommandLineInterface { Out = output, Error = error };

            int result = cli.Run(["download-schema", "--working-dir", directory.FullName]);

            Assert.Equal(1, result);
            Assert.Contains("download-schema", error.ToString());
            Assert.Throws<ObjectDisposedException>(() => AppServiceProvider.GetLogger<BootstrapHelpersTests>());
        }
        finally
        {
            AppServiceProvider.ServiceProvider = null!;
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void LoadExecutionContext_DisposesServicesWhenContextIsDisposed()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dbman-bootstrap-tests-");
        try
        {
            var rawConfig = new GenericStartup().GetDefaultConfig(directory.FullName);
            rawConfig.Logging = new LogSettings { Target = LogTarget.Console };
            var executionConfig = new ExecutionConfig
            {
                WorkingDirectory = directory.FullName,
                Dialect = SqlDialect.Generic,
                RawConfig = rawConfig
            };
            using var output = new StringWriter();

            {
                using ExecutionContext context = BootstrapHelpers.LoadExecutionContext(
                    executionConfig,
                    relaxed: false,
                    outputWriter: output);
                Assert.NotNull(AppServiceProvider.GetLogger<BootstrapHelpersTests>());
            }

            Assert.Throws<ObjectDisposedException>(() => AppServiceProvider.GetLogger<BootstrapHelpersTests>());
        }
        finally
        {
            AppServiceProvider.ServiceProvider = null!;
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadExecutionContext_UsesSharedHosting(bool hasConfigPath)
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

            using ExecutionContext context = BootstrapHelpers.LoadExecutionContext(
                executionConfig,
                relaxed: false,
                outputWriter: output);

            Assert.Same(output, context.Out);
            Assert.Equal(directory.FullName, context.WorkingDirectory);
            Assert.Equal(rootDirectory, context.Config.ProjectDirectory);
            Assert.True(context.Logger.IsEnabled(LogLevel.Debug));
            Assert.NotNull(context.SourceManager);
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

            using (ExecutionContext context = BootstrapHelpers.LoadExecutionContext(
                executionConfig,
                relaxed: false,
                outputWriter: new StringWriter()))
            {
                Assert.IsType<MsCommandRunner>(context.CommandRunner);
            }

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

    private sealed class ServiceCheckingWriter : StringWriter
    {
        public override void WriteLine(string? value)
        {
            Assert.NotNull(AppServiceProvider.GetLogger<BootstrapHelpersTests>());
            base.WriteLine(value);
        }
    }
}
