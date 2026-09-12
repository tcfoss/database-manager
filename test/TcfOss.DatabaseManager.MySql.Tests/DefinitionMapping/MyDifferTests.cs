using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class MyDifferTests : MyDifferTestsBase
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    protected override MyConfig LoadConfig(Config rawConfig, ILogger logger)
    {
        var otherData = GetOtherData(rawConfig);
        return new MyConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherData);
    }
}
