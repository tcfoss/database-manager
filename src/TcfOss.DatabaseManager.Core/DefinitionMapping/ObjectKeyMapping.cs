using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public record ObjectKeyMapping<T>(ObjectHandle Handle, ObjectIdentifier Name, T? Start, T? End);
