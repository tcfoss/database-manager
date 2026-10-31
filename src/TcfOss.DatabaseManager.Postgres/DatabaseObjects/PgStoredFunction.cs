using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgStoredFunction(ObjectIdentifier Name, SqlValueList<PgRoutineParameter> Parameters,
    DataType ReturnType, Statement Body) : IHaveObjectIdentifierName;
