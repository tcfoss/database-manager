using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class MyInfoSchemaRepoTests : BaseInfoSchemaRepoTests, IClassFixture<MyInfoSchemaContextFixture>
{
    private readonly FakeRawEntityRetriever _rawEntityRetriever = new();
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
        Repo = new InfoSchemaRepo(MyConfig, contextFactory, _rawEntityRetriever);
        // ReSharper restore VirtualMemberCallInConstructor
    }

    [Fact]
    public async Task Function_UsesCreateDefinitionForBody()
    {
        var id = ObIdent("func_no_params", "extra_test");
        const string body = "BEGIN\n    RETURN 'it\\'s \"quoted\"';\nEND";
        _rawEntityRetriever.FunctionDefinitions[("extra_test", "func_no_params")] = $"CREATE FUNCTION `func_no_params`() RETURNS INT {body}";

        var function = await Repo.GetFunctionAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal($" {body}", function.RawBodyText);
        var helper = new InfoSchemaRepoHelper(new MyLexer(), new MyParser(), MyConfig.QuoteStyle);
        Assert.Equal(helper.ParseStatement(body).ToSql(), function.Body.ToSql());
    }

    [Fact]
    public async Task Triggers_UseCreateDefinitionsAndKeepMetadata()
    {
        var table = ObIdent("book", "library_catalog");
        var id = ObIdent("tr_book_after_insert", "library_catalog");
        const string body = "BEGIN\n    SET NEW.description = 'it\\'s \"quoted\"';\nEND";
        _rawEntityRetriever.TriggerDefinitions[("library_catalog", "tr_book_after_insert")] = $"CREATE TRIGGER `tr_book_after_insert` AFTER INSERT ON `book` FOR EACH ROW {body}";

        var triggers = await Repo.GetTriggersOnTableAsync(table, TestContext.Current.CancellationToken);

        Assert.Equal(3, triggers.Count);
        Assert.Equal($" {body}", triggers[id].RawBodyText);
        Assert.Equal(1U, triggers[id].Order);
        var helper = new InfoSchemaRepoHelper(new MyLexer(), new MyParser(), MyConfig.QuoteStyle);
        Assert.Equal(helper.ParseStatement(body).ToSql(), triggers[id].Body.ToSql());
        Assert.Contains(ObIdent("tr_book_after_update", "library_catalog"), triggers.Keys);
    }

    [Fact]
    public async Task Function_WithoutCreateDefinitionFails()
    {
        var id = ObIdent("func_no_params", "extra_test");
        _rawEntityRetriever.FunctionDefinitions[("extra_test", "func_no_params")] = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.GetFunctionAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Trigger_WithoutCreateDefinitionFails()
    {
        var id = ObIdent("tr_book_after_insert", "library_catalog");
        _rawEntityRetriever.TriggerDefinitions[("library_catalog", "tr_book_after_insert")] = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo.GetTriggersOnTableAsync(ObIdent("book", "library_catalog"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Functions_PrefetchDefinitionsOncePerSchema()
    {
        var id = ObIdent("func_no_params", "extra_test");

        await Repo.GetFunctionAsync(id, TestContext.Current.CancellationToken);
        await Repo.GetFunctionAsync(id, TestContext.Current.CancellationToken);

        var batch = Assert.Single(_rawEntityRetriever.FunctionBatches);
        Assert.Equal(["func_no_params"], batch.Select(f => f.Name));
    }

    [Fact]
    public async Task Triggers_PrefetchDefinitionsOncePerSchema()
    {
        await Repo.GetTriggersOnTableAsync(ObIdent("book", "library_catalog"), TestContext.Current.CancellationToken);
        var genreTriggers = await Repo.GetTriggersOnTableAsync(ObIdent("genre", "library_catalog"), TestContext.Current.CancellationToken);

        var batch = Assert.Single(_rawEntityRetriever.TriggerBatches);
        Assert.Equal(11, batch.Count);
        Assert.Equal(2, genreTriggers.Count);
    }
}
