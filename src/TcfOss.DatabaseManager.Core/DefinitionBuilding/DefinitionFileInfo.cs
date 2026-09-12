using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public record struct DefinitionFileInfoSet(SchemaIdentifier SchemaId, IEnumerable<DefinitionFileInfo> Files);

public record struct DefinitionFileInfo(string FullPath, string RelativePath, string Text);

public record struct DefinitionFileInfoWithSchema(SchemaIdentifier SchemaId, string FullPath, string RelativePath, string Text);
