using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.MariaDb.Tests.Configuration;

public class MaConfigLoaderTests
{
    private static MyConfig LoadConfig(ConfigParsing.Config rawConfig, Dictionary<string, object>? otherInfo = null)
    {
        otherInfo ??= MaGetOtherData.GetData(rawConfig);
        return new MaConfigLoader(new LoggerFactory().CreateLogger<MaConfigLoader>()).LoadConfig(Path.GetFullPath("/home/username/database"), rawConfig, otherInfo);
    }

    [Fact]
    public void Credentials_ConnectionTimeout_Uses_Specified_Value()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials!.ConnectionTimeout = 60;
        var config = LoadConfig(rawConfig);

        Assert.Equal<uint>(60, config.DatabaseCredentials.ConnectionTimeout);
    }

    [Fact]
    public void Credentials_ConnectionTimeout_Uses_Default_When_Not_Set()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Credentials!.ConnectionTimeout = null;
        var config = LoadConfig(rawConfig);

        Assert.Equal<uint>(30, config.DatabaseCredentials.ConnectionTimeout);
    }

    [Fact]
    public void Attribute_Defaults_Non_Overrides_MariaDb()
    {
        var rawConfig = TestDataRawConfig.MyTestRawConfigSillyDefaults;
        rawConfig.Dialect = SqlDialect.MariaDb;
        var config = new MaConfigLoader(new LoggerFactory().CreateLogger<MaConfigLoader>()).LoadConfig("/home/username/database", rawConfig, MaGetOtherData.GetData(rawConfig));

        Assert.Equal<uint?>(3, config.AttributeDefaults.SignedIntWidth);
        Assert.Equal<uint?>(10, config.AttributeDefaults.UnsignedIntWidth);
        Assert.Equal<uint?>(20, config.AttributeDefaults.SignedBigIntWidth);
        Assert.Equal<uint?>(25, config.AttributeDefaults.UnsignedBigIntWidth);
        Assert.Equal<uint?>(9, config.AttributeDefaults.SignedMediumIntWidth);
        Assert.Equal<uint?>(8, config.AttributeDefaults.UnsignedMediumIntWidth);
        Assert.Equal<uint?>(6, config.AttributeDefaults.SignedSmallIntWidth);
        Assert.Equal<uint?>(5, config.AttributeDefaults.UnsignedSmallIntWidth);
        Assert.Equal<uint?>(4, config.AttributeDefaults.SignedTinyIntWidth);
        Assert.Equal<uint?>(3, config.AttributeDefaults.UnsignedTinyIntWidth);
        Assert.Equal(new NumericLength.PrecisionScale(10, 0), config.AttributeDefaults.DecimalPrecision);
        Assert.Null(config.AttributeDefaults.FloatPrecision);
        Assert.Null(config.AttributeDefaults.DoublePrecision);
        Assert.Equal<uint?>(4, config.AttributeDefaults.YearPrecision);
    }
}
