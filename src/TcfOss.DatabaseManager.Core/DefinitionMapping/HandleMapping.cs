using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public record HandleMapping<T>(Handle Name, T? Start, T? End);
