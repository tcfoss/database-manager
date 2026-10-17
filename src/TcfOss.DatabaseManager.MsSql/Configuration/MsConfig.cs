using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.MsSql.Configuration;

public record MsConfig : ConfigWithSchemaMapsBase<MsSchemaMapping>
{
    public required MsDatabaseCredentials DatabaseCredentials { get; init; }
    public required Version Version { get; init; }

    public string ConnectionString => DatabaseCredentials.ConnectionString;
}
