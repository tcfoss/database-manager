using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public interface INormalizeDataTypes
{
    DataType NormalizeDataType(DataType dataType);
}
