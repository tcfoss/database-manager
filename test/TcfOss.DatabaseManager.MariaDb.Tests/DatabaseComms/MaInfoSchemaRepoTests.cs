using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
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

    private static string DefaultCollation => " COLLATE utf8mb4_uca1400_ai_ci";

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

    [Fact]
    public async Task View()
    {
        var viewId = new ObjectIdentifier("available_books", new SchemaIdentifier("library_activity", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var view = await Repo.GetViewAsync(viewId, TestContext.Current.CancellationToken);
        Assert.NotNull(view);
        Assert.Equal("available_books", view.Name.Name);

        Assert.Equal(ViewAlgorithm.Undefined, view.Algorithm);
        Assert.Equal(SecurityContext.Invoker, view.SecurityContext);
        Assert.Equal(new Account.IdentityWithHost(new ExtendedIdentifier("admin", ExtendedQuoteStyle.Backticks), new ExtendedIdentifier("localhost", ExtendedQuoteStyle.Backticks)), view.Definer.Account);

        var actual = view.Body.ToSql();
        var expected = "SELECT `bc`.`book_id` AS `book_id`, `bc`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `bc` WHERE NOT EXISTS (SELECT 1 FROM `library_activity`.`active_rental` AS `ar` WHERE `ar`.`book_id` = `bc`.`book_id` AND `ar`.`really_active` = 'Y' LIMIT 1)";
        Assert.Equal(expected, actual, ignoreCase: true, ignoreAllWhiteSpace: true);
    }

    [Fact]
    public async Task Procedure()
    {
        var procedureId = new ObjectIdentifier("search_available_books", new SchemaIdentifier("library_activity", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var procedure = await Repo.GetProcedureAsync(procedureId, TestContext.Current.CancellationToken);
        Assert.NotNull(procedure);
        Assert.Equal("search_available_books", procedure.Name.Name);

        Assert.Equal(SecurityContext.Definer, procedure.SecurityContext);

        Assert.Equal(new Account.IdentityWithHost(new ExtendedIdentifier("admin", ExtendedQuoteStyle.Backticks), new ExtendedIdentifier("localhost", ExtendedQuoteStyle.Backticks)), procedure.Definer.Account);

        var parameters = procedure.Parameters.ToList();
        var parameter = Assert.Single(parameters);
        Assert.Equal("search_term", parameter.Name.Name);
        Assert.Equal($"VARCHAR(255) CHARACTER SET utf8mb4{DefaultCollation}", parameter.DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.In, Assert.IsType<RoutineParameter.Directed>(parameter).Direction);

        var expected = "BEGIN SELECT ab.book_id, ab.full_title, bs.title, bs.description, bs.authors, bs.genres FROM available_books AS ab INNER JOIN book_search AS bs ON ab.book_id = bs.book_id WHERE MATCH (bs.title, bs.description, bs.authors, bs.genres) AGAINST (search_term IN NATURAL LANGUAGE MODE) OR ab.full_title LIKE CONCAT('%', search_term, '%') OR bs.description LIKE CONCAT('%', search_term, '%'); END";
        var actual = procedure.Body.ToSql();

        Assert.Equal(expected, actual);
    }
}
