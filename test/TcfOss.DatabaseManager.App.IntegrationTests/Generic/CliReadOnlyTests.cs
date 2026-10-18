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

        var result = CliRunner.RunCli("validate-config", fileFixture, out var actual, out _);

        Assert.Equal(0, result);

        Assert.Equal("Database connection available: False", actual);
    }

    [Fact]
    public void ValidateConfig_PrintConfig()
    {
        using var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(SimpleSchemaDirectory.FullName);

        var result = CliRunner.RunCli("validate-config", fileFixture, out var actual, out _, "--print-config");

        Assert.Equal(0, result);

        Assert.Contains("Dialect: Generic", actual);
        Assert.Contains("""SchemaName: '"dev"."simple_schema"'""", actual);
        Assert.DoesNotContain("Database connection available:", actual);
    }

    [Fact]
    public void ValidateConfig_NoConfig_Fails()
    {
        using var fileFixture = new FsProjectFixture();

        var result = CliRunner.RunCli("validate-config", fileFixture, out _, out var actual);

        Assert.NotEqual(0, result);

        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigRequiredTemplate,
            "validate-config");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateConfig_SpecifiedConfig_DoesNotExist_Fails()
    {
        using var fileFixture = new FsProjectFixture();

        var result = CliRunner.RunCli(
            command: "validate-config",
            fileFixture: fileFixture,
            output: out _,
            error: out var actual,
            dialect: null,
            config: "database-manager.yml");

        Assert.NotEqual(0, result);

        var inferredPath = Path.Combine(fileFixture.RootDirectory.FullName, "database-manager.yml");
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigDoesNotExistTemplate,
            "validate-config", inferredPath);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateConfig_SpecifiedConfig_DoesNotExist_NoWorkingDir_Fails()
    {
        var result = CliRunner.RunCli(
            command: "validate-config",
            fileFixture: null,
            output: out _,
            error: out var actual,
            dialect: null,
            config: "database-manager.yml");

        Assert.NotEqual(0, result);

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

        var result = CliRunner.RunCli("download-schema", fileFixture, out _, out var actual);

        Assert.NotEqual(0, result);

        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.DialectRequiredTemplate,
            "download-schema");
        Assert.Equal(expected, actual);
    }

    private static DirectoryInfo SchemasDirectory { get; } = new(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "Generic"));
    private static DirectoryInfo SimpleSchemaDirectory { get; } = new(Path.Combine(SchemasDirectory.FullName, "SimpleSchema"));
}
