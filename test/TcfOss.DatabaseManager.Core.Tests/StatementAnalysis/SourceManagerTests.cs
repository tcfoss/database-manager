using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.StatementAnalysis;

public class SourceManagerTests
{
    private static ObjectHandle MakeHandle(string name)
    {
        ObjectIdentifier id = ObjectIdentifier.FromStrings("def", "myschema", name);
        return ObjectHandle.Create(id, NameHandling.None);
    }

    // === RegisterFileSource ===

    [Fact]
    public void RegisterFileSource_Returns_Unique_Ids()
    {
        var manager = new SourceManager();

        int id1 = manager.RegisterFileSource("/path/a.sql");
        int id2 = manager.RegisterFileSource("/path/b.sql");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void RegisterFileSource_Same_Path_Returns_Same_Id()
    {
        var manager = new SourceManager();

        int id1 = manager.RegisterFileSource("/path/a.sql");
        int id2 = manager.RegisterFileSource("/path/a.sql");

        Assert.Equal(id1, id2);
    }

    // === GetSourceById ===

    [Fact]
    public void GetSourceById_Returns_FileSource_After_Registration()
    {
        var manager = new SourceManager();
        int id = manager.RegisterFileSource("/path/test.sql");

        SqlSource? source = manager.GetSource(id);

        Assert.NotNull(source);
        var fileSource = Assert.IsType<SqlSource.FileSource>(source);
        Assert.Equal("/path/test.sql", fileSource.Path);
        Assert.Equal(id, fileSource.SourceId);
    }

    [Fact]
    public void GetSourceById_Returns_Null_For_Unknown_Id()
    {
        var manager = new SourceManager();

        SqlSource? source = manager.GetSource(999);

        Assert.Null(source);
    }

    // === RegisterObject + GetSourceByObject ===

    [Fact]
    public void GetSourceByObject_Returns_Source_After_Registration()
    {
        var manager = new SourceManager();
        int sourceId = manager.RegisterFileSource("/path/tables.sql");
        ObjectHandle handle = MakeHandle("my_table");

        manager.RegisterObject(handle, new SourceRef(sourceId, 10, 50));

        SqlSource? source = manager.GetSource(handle);
        Assert.NotNull(source);
        Assert.Equal(sourceId, source.SourceId);
    }

    [Fact]
    public void GetSourceByObject_Returns_Null_When_Not_Registered()
    {
        var manager = new SourceManager();

        SqlSource? source = manager.GetSource(MakeHandle("unknown"));

        Assert.Null(source);
    }
}
