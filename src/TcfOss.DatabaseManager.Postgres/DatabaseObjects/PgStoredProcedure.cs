using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgStoredProcedure(ObjectIdentifier Name, SqlValueList<PgRoutineParameter> Parameters,
    Statement Body) : IHaveObjectIdentifierName;
