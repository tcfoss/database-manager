using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.Postgres.Configuration;

public record PgConfig : ConfigWithSchemaMapsBase<PgSchemaMapping>
{
    public SqlValueList<Identifier> SearchPath { get; init; } = [new("public")];
}
