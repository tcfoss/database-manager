using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.IntegrationTests;

public class FileWriterTests
{
    private DirectoryInfo RootDirectory { get; }
    private FileWriter FileWriter { get; }

    public FileWriterTests()
    {
        RootDirectory = new DirectoryInfo(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(RootDirectory.FullName);
        FileWriter = new FileWriter(new LoggerFactory().CreateLogger<FileWriter>());
    }

    [Fact]
    public void WriteTextToFile_Skip()
    {
        File.WriteAllText(Path.Combine(RootDirectory.FullName, "testfile.txt"), "Initial content");

        FileWriter.WriteTextToFile(Path.Combine(RootDirectory.FullName, "testfile.txt"), "New content", FileExistsAction.Skip);

        Assert.Equal("Initial content", File.ReadAllText(Path.Combine(RootDirectory.FullName, "testfile.txt")));
    }

    [Fact]
    public void WriteTextToFile_Overwrite()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        FileWriter.WriteTextToFile(filePath, "New content", FileExistsAction.Overwrite);

        Assert.Equal("New content", File.ReadAllText(filePath));
    }

    [Fact]
    public void WriteTextToFile_Rename()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        FileWriter.WriteTextToFile(filePath, "New content", FileExistsAction.Rename);

        Assert.Equal("New content", File.ReadAllText(filePath));

        var bakFilePath = Path.Combine(RootDirectory.FullName, "testfile.txt.bak.1");
        Assert.True(File.Exists(bakFilePath));
        Assert.Equal("Initial content", File.ReadAllText(bakFilePath));
    }

    [Fact]
    public void WriteTextToFile_Rename_MultipleExist()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        var preexistingBakFile = Path.Combine(RootDirectory.FullName, "testfile.txt.bak.10");
        File.WriteAllText(preexistingBakFile, "Preexisting backup content");


        FileWriter.WriteTextToFile(filePath, "New content", FileExistsAction.Rename);

        Assert.Equal("New content", File.ReadAllText(filePath));

        var bakFilePath = Path.Combine(RootDirectory.FullName, "testfile.txt.bak.11");
        Assert.True(File.Exists(bakFilePath));
        Assert.Equal("Initial content", File.ReadAllText(bakFilePath));
    }

    [Fact]
    public void WriteTextToFile_Error()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        Assert.Throws<CommandException.FileExists>(() =>
            FileWriter.WriteTextToFile(filePath, "New content", FileExistsAction.Error));
    }

    [Fact]
    public void GetFileStreamWriter_Skip()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        StreamWriter? streamWriter;

        using (streamWriter = FileWriter.GetFileStreamWriter(filePath, FileExistsAction.Skip))
        {
            Assert.Null(streamWriter);
        }
        Assert.Equal("Initial content", File.ReadAllText(filePath));
    }

    [Fact]
    public void GetFileStreamWriter_Overwrite()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        using (var streamWriter = FileWriter.GetFileStreamWriter(filePath, FileExistsAction.Overwrite))
        {
            Assert.NotNull(streamWriter);
            streamWriter.Write("New content");
        }
        Assert.Equal("New content", File.ReadAllText(filePath));
    }

    [Fact]
    public void GetFileStreamWriter_Rename()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        using (var streamWriter = FileWriter.GetFileStreamWriter(filePath, FileExistsAction.Rename))
        {
            Assert.NotNull(streamWriter);
            streamWriter.Write("New content");
        }
        Assert.Equal("New content", File.ReadAllText(filePath));

        var bakFilePath = Path.Combine(RootDirectory.FullName, "testfile.txt.bak.1");
        Assert.True(File.Exists(bakFilePath));
        Assert.Equal("Initial content", File.ReadAllText(bakFilePath));
    }

    [Fact]
    public void GetFileStreamWriter_Rename_MultipleExist()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        var preexistingBakFile = Path.Combine(RootDirectory.FullName, "testfile.txt.bak.10");
        File.WriteAllText(preexistingBakFile, "Preexisting backup content");

        using (var streamWriter = FileWriter.GetFileStreamWriter(filePath, FileExistsAction.Rename))
        {
            Assert.NotNull(streamWriter);
            streamWriter.Write("New content");
        }

        FileWriter.WriteTextToFile(filePath, "New content", FileExistsAction.Rename);

        Assert.Equal("New content", File.ReadAllText(filePath));

        var bakFilePath = Path.Combine(RootDirectory.FullName, "testfile.txt.bak.11");
        Assert.True(File.Exists(bakFilePath));
        Assert.Equal("Initial content", File.ReadAllText(bakFilePath));
    }

    [Fact]
    public void GetFileStreamWriter_Error()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "testfile.txt");
        File.WriteAllText(filePath, "Initial content");

        Assert.Throws<CommandException.FileExists>(() =>
            FileWriter.GetFileStreamWriter(filePath, FileExistsAction.Error));
    }

    [Fact]
    public void RenameExistingFile_DoesNothingOnNotFound()
    {
        var filePath = Path.Combine(RootDirectory.FullName, "nonexistent.txt");
        FileWriter.RenameExistingFile(filePath);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void RenameExistingFile_DirectoryNotExist_Throws()
    {
        var newRoot = Path.Combine(RootDirectory.FullName, "nonexistent_directory");
        var filePath = Path.Combine(newRoot, "testfile.txt");

        Assert.Throws<DirectoryNotFoundException>(() => FileWriter.RenameExistingFile(filePath));
    }
}
