using Microsoft.Data.SqlClient;
using TcfOss.DatabaseManager.Core.Configuration;

namespace TcfOss.DatabaseManager.MsSql.Configuration;

public class MsConfig : ConfigWithSchemaMapsBase<MsSchemaMapping>
{
    public required MsDatabaseCredentials DatabaseCredentials { get; init; }
    public required Version Version { get; init; }

    /// <summary>
    /// Builds a connection string targeting the <c>master</c> database, used
    /// for reading server metadata.
    /// </summary>
    public string ConnectionString
    {
        get
        {
            var csb = new SqlConnectionStringBuilder
            {
                DataSource = DatabaseCredentials.DataSource,
                InitialCatalog = "master",
                Encrypt = DatabaseCredentials.Encrypt,
                TrustServerCertificate = DatabaseCredentials.TrustServerCertificate,
                ConnectTimeout = DatabaseCredentials.ConnectionTimeout,
                MinPoolSize = (int)MinPoolSize,
                MaxPoolSize = (int)MaxPoolSize,
            };

            if (DatabaseCredentials.Username != null)
            {
                csb.UserID = DatabaseCredentials.Username;
                csb.Password = DatabaseCredentials.Password;
            }
            else
            {
                csb.IntegratedSecurity = true;
            }

            return csb.ConnectionString;
        }
    }
}
