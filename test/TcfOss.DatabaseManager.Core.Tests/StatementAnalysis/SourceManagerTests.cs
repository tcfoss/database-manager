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

    [Fact]
    public void GetSourceRef_Returns_Registered_Reference()
    {
        var manager = new SourceManager();
        ObjectHandle handle = MakeHandle("my_table");
        int sourceId = manager.RegisterFileSource("/path/tables.sql");
        var sourceRef = new SourceRef(sourceId, 10, 50);

        manager.RegisterObject(handle, sourceRef);

        Assert.Equal(sourceRef, manager.GetSourceRef(handle));
    }

    [Fact]
    public void GetSourceRef_Returns_Null_When_Not_Registered()
    {
        var manager = new SourceManager();

        Assert.Null(manager.GetSourceRef(MakeHandle("unknown")));
    }

    [Fact]
    public void RegisterObject_Replaces_Previous_Reference()
    {
        var manager = new SourceManager();
        ObjectHandle handle = MakeHandle("my_table");
        int firstSourceId = manager.RegisterFileSource("/path/first.sql");
        int secondSourceId = manager.RegisterFileSource("/path/second.sql");
        manager.RegisterObject(handle, new SourceRef(firstSourceId, 10, 50));
        var replacement = new SourceRef(secondSourceId, 20, 60);

        manager.RegisterObject(handle, replacement);

        Assert.Equal(replacement, manager.GetSourceRef(handle));
        Assert.Equal(manager.GetSource(secondSourceId), manager.GetSource(handle));
    }

    [Fact]
    public void GetSourceByObject_Returns_Null_For_Unknown_Source_Id()
    {
        var manager = new SourceManager();
        ObjectHandle handle = MakeHandle("my_table");
        manager.RegisterObject(handle, new SourceRef(999, 10, 50));

        Assert.Null(manager.GetSource(handle));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetFilename_Returns_Registered_Path(bool registered)
    {
        var manager = new SourceManager();
        int sourceId = manager.RegisterFileSource("/path/tables.sql");
        var sourceRef = new SourceRef(registered ? sourceId : 999, 10, 50);
        string? expected = registered ? "/path/tables.sql" : null;

        Assert.Equal(expected, manager.GetFilename(sourceRef));
        Assert.Equal(expected, manager.GetFilename((SourceRef?)sourceRef));
    }

    [Fact]
    public void GetFilename_Returns_Null_For_Null_Reference()
    {
        var manager = new SourceManager();

        Assert.Null(manager.GetFilename(null));
    }

    [Theory]
    [InlineData(2, 8, "SELECT")]
    [InlineData(0, 11, "  SELECT 1;")]
    [InlineData(2, 2, "")]
    [InlineData(11, 11, "")]
    public void GetText_Returns_Metadata_Range(int start, int end, string expected)
    {
        const string body = "  SELECT 1;";
        var meta = new MetaData { Start = start, End = end };
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, body);
            var manager = new SourceManager();
            int sourceId = manager.RegisterFileSource(path);
            var sourceRef = new SourceRef(sourceId, 0, body.Length);

            Assert.Equal(expected, manager.GetText(sourceRef, meta));
            Assert.Equal(expected, SourceManager.GetText(body, meta));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false, 0, 1)]
    [InlineData(true, null, 1)]
    [InlineData(true, 0, null)]
    [InlineData(true, null, null)]
    public void GetText_Returns_Null_For_Missing_Input(bool hasInput, int? start, int? end)
    {
        var manager = new SourceManager();
        SourceRef? sourceRef = hasInput ? new SourceRef(999, 0, 1) : null;
        string? body = hasInput ? "SELECT 1;" : null;
        var meta = new MetaData { Start = start, End = end };

        Assert.Null(manager.GetText(sourceRef, meta));
        Assert.Null(SourceManager.GetText(body, meta));
    }

    [Fact]
    public void GetText_Returns_Null_For_Null_Metadata()
    {
        var manager = new SourceManager();
        var sourceRef = new SourceRef(999, 0, 1);

        Assert.Null(manager.GetText(sourceRef, null));
        Assert.Null(SourceManager.GetText("SELECT 1;", null));
    }

    [Fact]
    public void GetText_Returns_Null_For_Unknown_Source_Id()
    {
        var manager = new SourceManager();
        var sourceRef = new SourceRef(999, 0, 1);
        var meta = new MetaData { Start = 0, End = 1 };

        Assert.Null(manager.GetText(sourceRef, meta));
    }
}
