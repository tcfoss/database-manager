using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;

namespace TcfOss.DatabaseManager.App.IntegrationTests;

public static class CliRunner
{
    public static int RunCli(string command,
        FsProjectFixture? fileFixture,
        out string output,
        out string error,
        SqlDialect? dialect,
        string? config,
        params string[] args)
    {
        List<string> arguments = [];
        if (fileFixture != null)
        {
            arguments.Add("--working-dir");
            arguments.Add(fileFixture.RootDirectory.FullName);
        }
        if (config != null)
        {
            arguments.Add("--config");
            arguments.Add(config);
        }
        if (dialect.HasValue)
        {
            arguments.Add("--dialect");
            arguments.Add(dialect.Value.ToString());
        }

        arguments.Add(command);
        arguments.AddRange(args);

        var errorWriter = new StringWriter();
        var outputWriter = new StringWriter();

        var cli = new CommandLineInterface()
        {
            Out = outputWriter,
            Error = errorWriter
        };

        var exitCode = cli.Run([.. arguments]);

        output = outputWriter.ToString().Trim();
        error = errorWriter.ToString().Trim();

        return exitCode;
    }

    public static int RunCli(
        string command,
        FsProjectFixture fileFixture,
        out string output,
        out string error,
        params string[] args)
    {
        return RunCli(command, fileFixture, out output, out error, null, null, args);
    }

    public static int RunCli(
        string command,
        FsProjectFixture fileFixture,
        SqlDialect dialect,
        params string[] args)
    {
        return RunCli(command, fileFixture, out _, out _, dialect, null, args);
    }

    public static int RunCli(
        string command,
        FsProjectFixture fileFixture,
        SqlDialect dialect,
        out string output,
        out string error,
        params string[] args)
    {
        return RunCli(command, fileFixture, out output, out error, dialect, null, args);
    }
}
