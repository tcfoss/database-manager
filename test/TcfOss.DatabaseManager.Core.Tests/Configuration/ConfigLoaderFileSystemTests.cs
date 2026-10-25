using System.IO.Abstractions.TestingHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.Configuration;

public class ConfigLoaderFileSystemTests
{
    [Fact]
    public void DeployScriptDirectory_ExpandsSqlFilesRecursivelyInPathOrder()
    {
        var fileSystem = new MockFileSystem();
        string projectRoot = GetProjectRoot();
        string schemaRoot = Path.Combine(projectRoot, "schema");
        fileSystem.Directory.CreateDirectory(Path.Combine(schemaRoot, "Scripts", "nested"));
        fileSystem.File.WriteAllText(Path.Combine(schemaRoot, "Scripts", "z.sql"), "-- z");
        fileSystem.File.WriteAllText(Path.Combine(schemaRoot, "Scripts", "nested", "a.sql"), "-- a");
        fileSystem.File.WriteAllText(Path.Combine(schemaRoot, "Scripts", "readme.txt"), "ignored");

        var config = LoadConfig(fileSystem, projectRoot,
        [
            new ConfigParsing.DeployScript
            {
                FilePath = "Scripts",
                Type = DeployScriptType.PostDeployment,
                UniqueId = "directory-id",
            }
        ]);

        var scripts = Assert.Single(config.Schemas.Values).DeployScripts;
        Assert.Collection(
            scripts,
            script =>
            {
                Assert.Equal("a.sql", script.FileName);
                Assert.Equal(Path.Combine(schemaRoot, "Scripts", "nested", "a.sql"), script.FilePath.FullName);
                Assert.Equal(DeployScriptType.PostDeployment, script.Type);
                Assert.Equal("directory-id", script.UniqueId);
            },
            script =>
            {
                Assert.Equal("z.sql", script.FileName);
                Assert.Equal(Path.Combine(schemaRoot, "Scripts", "z.sql"), script.FilePath.FullName);
                Assert.Equal(DeployScriptType.PostDeployment, script.Type);
                Assert.Equal("directory-id", script.UniqueId);
            });
    }

    [Fact]
    public void DeployScriptManifest_ResolvesRelativeToManifestThenSchemaRoot()
    {
        var fileSystem = new MockFileSystem();
        string projectRoot = GetProjectRoot();
        string schemaRoot = Path.Combine(projectRoot, "schema");
        string manifestDirectory = Path.Combine(schemaRoot, "Manifests");
        fileSystem.Directory.CreateDirectory(Path.Combine(manifestDirectory, "Scripts"));
        fileSystem.File.WriteAllText(
            Path.Combine(manifestDirectory, "deploy.yaml"),
            """
            - FilePath: Scripts/relative.sql
            - FilePath: schema-only.sql
            """);
        fileSystem.File.WriteAllText(Path.Combine(manifestDirectory, "Scripts", "relative.sql"), "-- relative");
        fileSystem.File.WriteAllText(Path.Combine(schemaRoot, "schema-only.sql"), "-- schema root");

        var config = LoadConfig(fileSystem, projectRoot,
        [
            new ConfigParsing.DeployScript
            {
                FilePath = "Manifests/deploy.yaml",
                Type = DeployScriptType.PreDeployment,
                UniqueId = "manifest-id",
            }
        ]);

        var scripts = Assert.Single(config.Schemas.Values).DeployScripts;
        Assert.Collection(
            scripts,
            script =>
            {
                Assert.Equal(Path.Combine(manifestDirectory, "Scripts", "relative.sql"), script.FilePath.FullName);
                Assert.Equal(DeployScriptType.PreDeployment, script.Type);
                Assert.Equal("manifest-id", script.UniqueId);
            },
            script =>
            {
                Assert.Equal(Path.Combine(schemaRoot, "schema-only.sql"), script.FilePath.FullName);
                Assert.Equal(DeployScriptType.PreDeployment, script.Type);
                Assert.Equal("manifest-id", script.UniqueId);
            });
    }

    [Fact]
    public void RelativeDeployScriptPath_ResolvesFromSchemaRootEvenIfItExistsInCurrentDirectory()
    {
        var fileSystem = new MockFileSystem();
        string projectRoot = GetProjectRoot();
        string schemaRoot = Path.Combine(projectRoot, "schema");
        string relativePath = Path.Combine("deploy-script-resolution", "script.sql");
        string currentDirectoryPath = Path.Combine(fileSystem.Directory.GetCurrentDirectory(), relativePath);
        string schemaFilePath = Path.Combine(schemaRoot, relativePath);
        fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(currentDirectoryPath)!);
        fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(schemaFilePath)!);
        fileSystem.File.WriteAllText(currentDirectoryPath, "-- current directory");
        fileSystem.File.WriteAllText(schemaFilePath, "-- schema root");

        var config = LoadConfig(fileSystem, projectRoot,
        [
            new ConfigParsing.DeployScript
            {
                FilePath = relativePath,
                Type = DeployScriptType.PreDeployment,
            }
        ]);

        var script = Assert.Single(Assert.Single(config.Schemas.Values).DeployScripts);
        Assert.Equal(schemaFilePath, script.FilePath.FullName);
    }

    [Fact]
    public void RefactorFiles_AreReadThroughInjectedFileSystem()
    {
        var fileSystem = new MockFileSystem();
        string projectRoot = GetProjectRoot();
        string schemaRoot = Path.Combine(projectRoot, "schema");
        fileSystem.Directory.CreateDirectory(schemaRoot);
        fileSystem.File.WriteAllText(
            Path.Combine(schemaRoot, "refactors.yaml"),
            """
            - UniqueId: table-rename
              Type: TableRename
              OldName: old_table
              NewName: new_table
            """);

        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = "schema",
                    RefactorFiles = ["refactors.yaml"],
                }
            ]
        };

        var loader = new GenericConfigLoader(NullLogger<GenericConfigLoader>.Instance, fileSystem);
        var config = loader.LoadConfig(projectRoot, rawConfig, []);

        var refactor = Assert.IsType<Refactor.TableRename>(Assert.Single(Assert.Single(config.Schemas.Values).Refactors));
        Assert.Equal("table-rename", refactor.UniqueId);
        Assert.Equal("old_table", refactor.OldName.Name);
        Assert.Equal("new_table", refactor.NewName.Name);
    }

    private static GenericConfig LoadConfig(
        MockFileSystem fileSystem,
        string projectRoot,
        ConfigParsing.DeployScript[] deployScripts)
    {
        var rawConfig = new ConfigParsing.Config
        {
            Dialect = SqlDialect.Generic,
            Schemas =
            [
                new ConfigParsing.SchemaMapping
                {
                    SchemaName = "test_schema",
                    RootPath = "schema",
                    DeployScripts = deployScripts,
                }
            ]
        };

        var loader = new GenericConfigLoader(NullLogger<GenericConfigLoader>.Instance, fileSystem);
        return loader.LoadConfig(projectRoot, rawConfig, []);
    }

    private static string GetProjectRoot() =>
        Path.Combine(Path.GetTempPath(), "dbman-in-memory-tests", Guid.NewGuid().ToString("N"));
}
