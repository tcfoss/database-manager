using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Tests.Configuration;

namespace TcfOss.DatabaseManager.Core.Tests.DefinitionBuilding;

public class DefinitionBuildingTests
{
    [Fact]
    public void Test_File_Loader()
    {
        // Arrange
        var loggerFactory = new LoggerFactory();
        var config = new ConfigLoader(loggerFactory.CreateLogger<ConfigLoader>()).LoadConfig("database", TestDataRawConfig.MyTestRawConfig, []);
        var logger = loggerFactory.CreateLogger<DefinitionFileLoader<SchemaMappingBase>>();
        var fileLoader = new FakeFileLoader<SchemaMappingBase>(config, logger);

        // Act
        var files = fileLoader.GetFiles().ToList();

        // Assert
        Assert.NotNull(files);
        Assert.NotEmpty(files);
        Assert.Equal(2, files.Count);

        var (schema1, schema1Files) = (files[0].SchemaId, files[0].Files.ToList());
        Assert.Equal("schema1", schema1.Name);
        Assert.Equal(3, schema1Files.Count);
        Assert.DoesNotContain(schema1Files, f => f.FullPath.Contains("badfile.sql"));
        Assert.Contains(schema1Files, f => f.FullPath.EndsWith("file1.sql"));
        Assert.Contains(schema1Files, f => f.FullPath.EndsWith("file2.sql"));
        Assert.Contains(schema1Files, f => f.FullPath.EndsWith("file3.sql"));
        Assert.Contains(schema1Files, f => f.RelativePath == "file1.sql");
        Assert.Contains(schema1Files, f => f.RelativePath == Path.Combine("subdir", "file3.sql"));

        var (schema2, schema2Files) = (files[1].SchemaId, files[1].Files.ToList());
        Assert.Equal("schema2", schema2.Name);
        Assert.Equal(2, schema2Files.Count);
        Assert.Contains(schema2Files, f => f.FullPath.EndsWith(Path.Combine("subdir", "file4.sql")));
        Assert.Contains(schema2Files, f => f.FullPath.EndsWith("file5.sql"));
        Assert.Contains(schema2Files, f => f.RelativePath == Path.Combine("subdir", "file4.sql"));
        Assert.Contains(schema2Files, f => f.RelativePath == "file5.sql");
    }
}
