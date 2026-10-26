using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public record struct DefinitionAlterStatement(
    DefinitionAlterPhase Weight,
    SchemaIdentifier Schema,
    Statement Statement,
    string? Comment = null,
    bool FromDeployScript = false,
    uint SubWeight = 0);
