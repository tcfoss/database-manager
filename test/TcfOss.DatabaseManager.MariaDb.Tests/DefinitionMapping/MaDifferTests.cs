using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.MariaDb.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Tests.DefinitionMapping;

namespace TcfOss.DatabaseManager.MariaDb.Tests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class MaDifferTests : MyDifferTestsBase
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    protected override MyConfig LoadConfig(Config rawConfig, ILogger logger)
    {
        var otherData = GetOtherData(rawConfig);
        return new MaConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherData);
    }

}
