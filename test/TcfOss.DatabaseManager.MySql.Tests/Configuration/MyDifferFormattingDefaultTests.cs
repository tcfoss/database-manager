using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Tests.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.Configuration;

// ReSharper disable once UnusedMember.Global
public class MyDifferFormattingDefaultTests : DifferFormattingDefaultsTests
{
    protected override bool UseDelimiterAroundProgramsDefault => true;

    public override ConfigParsing.Config GetRawConfig()
    {
        return TestDataRawConfig.MyTestRawConfig;
    }

    public override ConfigBase GetConfig(ConfigParsing.Config rawConfig)
    {
        return new MyConfigLoader(new LoggerFactory().CreateLogger<MyConfigLoader>())
            .LoadConfig("/home/username/database", rawConfig, MyGetOtherData.GetData(rawConfig));
    }
}
