using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.MariaDb.Configuration;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;
using TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

using InfoSchemaRepo = TcfOss.DatabaseManager.MariaDb.DatabaseComms.InfoSchemaRepo;

namespace TcfOss.DatabaseManager.MariaDb.Tests.DatabaseComms;

public class MaInfoSchemaRepoTests : BaseInfoSchemaRepoTests, IClassFixture<MaInfoSchemaContextFixture>
{
    protected override MyConfig MyConfig { get; }
    protected override IRetrieveDatabaseObjects Repo { get; }
    protected override ReferentialAction DefaultReferentialAction => ReferentialAction.Restrict;
    protected override string DefaultIntegerWidth => "(11)";
    protected override RoutineParameterDirection FunctionParameterDirection => RoutineParameterDirection.In;
    protected override EventEnabledStatus DisableOnSlaveStatus => EventEnabledStatus.DisableOnSlave;

    protected override string DefaultCollation => " COLLATE utf8mb4_uca1400_ai_ci";
    protected override ViewAlgorithm? DefaultViewAlgorithm => ViewAlgorithm.Undefined;
    protected override string ViewExpectedText => "SELECT `bc`.`book_id` AS `book_id`, `bc`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `bc` WHERE NOT EXISTS (SELECT 1 FROM `library_activity`.`active_rental` AS `ar` WHERE `ar`.`book_id` = `bc`.`book_id` AND `ar`.`really_active` = 'Y' LIMIT 1)";
    protected override string ExpectedLowercaseFunctionName => "LCASE";

    public MaInfoSchemaRepoTests(MaInfoSchemaContextFixture fixture)
    {
        // ReSharper disable VirtualMemberCallInConstructor
        var contextFactory = fixture.CreateFactory();

        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Dialect = SqlDialect.MariaDb;
        rawConfig.Catalog = "def";
        rawConfig.ProjectDirectory = "/fake/project/root";
        var otherInfo = MyGetOtherData.GetData(rawConfig);
        MyConfig = new MaConfigLoader(new LoggerFactory().CreateLogger<MaConfigLoader>()).LoadConfig("/home/username/database", rawConfig, otherInfo);
        Repo = new InfoSchemaRepo(MyConfig, contextFactory, new FakeRawEntityRetriever());
        // ReSharper restore VirtualMemberCallInConstructor
    }
}
