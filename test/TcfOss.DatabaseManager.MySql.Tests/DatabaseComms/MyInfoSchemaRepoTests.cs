using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class MyInfoSchemaRepoTests : BaseInfoSchemaRepoTests, IClassFixture<MyInfoSchemaContextFixture>
{
    protected override MyConfig MyConfig { get; }
    protected override IRetrieveDatabaseObjects Repo { get; }
    protected override string ExpectedGenerationExpression => "concat(concat_ws(': ',`title`,`subtitle`),if((`publication_year` is not null),concat(' (',`publication_year`,')'),''))";

    protected override string DefaultCollation => " COLLATE utf8mb4_0900_ai_ci";
    protected override ViewAlgorithm? DefaultViewAlgorithm => null;
    protected override string ViewExpectedText => "SELECT `bc`.`book_id` AS `book_id`, `bc`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `bc` WHERE EXISTS (SELECT 1 FROM `library_activity`.`active_rental` AS `ar` WHERE ((`ar`.`book_id` = `bc`.`book_id`) AND (`ar`.`really_active` = 'Y'))) IS FALSE";
    protected override string ExpectedLowercaseFunctionName => "LOWER";

    public MyInfoSchemaRepoTests(MyInfoSchemaContextFixture fixture)
    {
        // ReSharper disable VirtualMemberCallInConstructor
        var contextFactory = fixture.CreateFactory();

        var rawConfig = TestDataRawConfig.MyTestRawConfig;
        rawConfig.Dialect = SqlDialect.MySql;
        rawConfig.Catalog = "def";
        rawConfig.ProjectDirectory = "/fake/project/root";
        var otherData = MyGetOtherData.GetData(rawConfig);
        MyConfig = new MyConfigLoader(new LoggerFactory().CreateLogger<MyConfigLoader>()).LoadConfig("/home/username/database", rawConfig, otherData);
        Repo = new InfoSchemaRepo(MyConfig, contextFactory, new FakeRawEntityRetriever());
        // ReSharper restore VirtualMemberCallInConstructor
    }
}
