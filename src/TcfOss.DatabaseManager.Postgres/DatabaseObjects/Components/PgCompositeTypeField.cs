using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgCompositeTypeField(Identifier Name, DataType DataType, ObjectName? Collation = null);
