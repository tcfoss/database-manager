using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.App;

public class MsCommandRunner(
    MsConfig config,
    IParser parser,
    ILexer lexer,
    IFormatSqlFiles sqlFormatter,
    IWriteFiles fileWriter
) : CommandRunner<MsConfig>(config, fileWriter, sqlFormatter)
{
    private readonly IParser _parser = parser;
    private readonly ILexer _lexer = lexer;

    // void ParseFiles: base implementation will do.

    public override void ParseDefinition(string outputPath, bool relaxed = false, bool includeMeta = false, bool includeRawText = false, bool includeSourceRef = false, FileExistsAction fileExistsAction = FileExistsAction.Error)
    {
        throw new NotImplementedException("ParseDefinition is not implemented for MsCommandRunner yet.");
    }

    public override Task DownloadSchemaAsync()
    {
        throw new NotImplementedException("DownloadSchemaAsync is not implemented for MsCommandRunner yet.");
    }

    public override Task ComputeChangesAsync(string? outputPath, FileExistsAction fileExistsAction = FileExistsAction.Error)
    {
        throw new NotImplementedException("ComputeChangesAsync is not implemented for MsCommandRunner yet.");
    }

    // void FormatSql: base implementation will do.

    protected override FileParser CreateFileParser()
    {
        return new FileParser(_lexer, _parser);
    }
}
