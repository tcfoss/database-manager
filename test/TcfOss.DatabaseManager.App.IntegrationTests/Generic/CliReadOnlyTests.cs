using System.Globalization;
using DotNet.Testcontainers.Builders;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.Tests;

namespace TcfOss.DatabaseManager.App.IntegrationTests.Generic;

public class CliReadOnlyTests
{
    [Fact]
    public void ValidateConfig()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(SimpleSchemaDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Out = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config"
        ]);

        Assert.Equal(0, result);

        Assert.Equal("Database connection available: False", writer.ToString().Trim());
    }

    [Fact]
    public void ValidateConfig_PrintConfig()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(SimpleSchemaDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Out = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
            "--print-config"
        ]);

        Assert.Equal(0, result);

        var actual = writer.ToString();

        Assert.Contains("Dialect: Generic", actual);
        Assert.Contains("""SchemaName: '"dev"."simple_schema"'""", actual);
        Assert.DoesNotContain("Database connection available:", actual);
    }

    [Fact]
    public void ValidateConfig_NoConfig_Fails()
    {
        using var fileFixture = new FsProjectFixture();

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config"
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigRequiredTemplate,
            "validate-config");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateConfig_SpecifiedConfig_DoesNotExist_Fails()
    {
        using var fileFixture = new FsProjectFixture();

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--config",
            "database-manager.yml",
            "validate-config",
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var inferredPath = Path.Combine(fileFixture.RootDirectory.FullName, "database-manager.yml");
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigDoesNotExistTemplate,
            "validate-config", inferredPath);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateConfig_SpecifiedConfig_DoesNotExist_NoWorkingDir_Fails()
    {
        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--config",
            "database-manager.yml",
            "validate-config"
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var inferredPath = Path.Combine(Directory.GetCurrentDirectory(), "database-manager.yml");
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigDoesNotExistTemplate,
            "validate-config", inferredPath);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DownloadSchema_Fails()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(SimpleSchemaDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "download-schema"
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.DialectRequiredTemplate,
            "download-schema");
        Assert.Equal(expected, actual);
    }

    private static DirectoryInfo SchemasDirectory { get; } = new(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "Generic"));
    private static DirectoryInfo SimpleSchemaDirectory { get; } = new(Path.Combine(SchemasDirectory.FullName, "SimpleSchema"));
}
