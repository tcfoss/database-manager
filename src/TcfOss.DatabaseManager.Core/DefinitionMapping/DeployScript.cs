using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public record DeployScript(DeployScriptType Type, SchemaIdentifier SchemaId, string FileName, string FullPath, SqlValueList<Statement> Body)
{
    public string? UniqueId { get; init; }
    public string? RawBodyText { get; init; }
}
