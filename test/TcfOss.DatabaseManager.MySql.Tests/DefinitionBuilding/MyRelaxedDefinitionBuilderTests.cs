using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using TcfOss.DatabaseManager.MySql.Tests.Configuration;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;


// ReSharper disable once UnusedMember.Global
public class MyRelaxedDefinitionBuilderTests : MyDefinitionBuilderTests
{
    protected override bool Relaxed => true;
    protected override Func<MyConfig, ILogger<MyDefinitionBuilder>, SourceManager, MyDefinitionBuilder> DefinitionBuilderFactory => (config, logger, sourceManager) => new MyRelaxedDefinitionBuilder(config, sourceManager, new MyFunctionNameProvider(), logger);

    private (MyConfig Config, MyDefinitionBuilder Builder, TextParser Parser) GetCustomDefinitionBuilder(ValidationSettings validationSettings)
    {
        var rawConfig = TestDataRawConfig.GetMyTestRawConfig();


        var logger = new LoggerFactory().CreateLogger<MyDefinitionBuilder>();
        var otherInfo = MyGetOtherData.GetData(rawConfig);
        var config = new MyConfigLoader(logger).LoadConfig("/home/username/database", rawConfig, otherInfo, Relaxed);
        config = config with { ValidationSettings = validationSettings };

        var builder = new MyRelaxedDefinitionBuilder(config, new SourceManager(), new MyFunctionNameProvider(), logger);
        var parser = new TextParser(new MyLexer(), new MyParser());

        return (config, builder, parser);
    }

    [Fact]
    public void MissingDefiner_Allowed_DefaultsToCurrentDefiner()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowMissingDefiner = true });
        var text = """
        CREATE PROCEDURE my_procedure()
        SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END
        """;

        var statements = parser.ParseText(text);
        builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        var def = builder.ToDefinition();
        var proc = def.Procedures.First().Value;
        Assert.NotNull(proc);
        Assert.Equal(new Account.CurrentUser(), proc.Definer.Account);
    }

    [Fact]
    public void MissingDefiner_NotAllowed_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowMissingDefiner = false });
        var text = """
        CREATE PROCEDURE my_procedure()
        SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.MissingDefiner>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_procedure", exception.Message);
    }

    [Fact]
    public void ImplicitDefiner_Allowed()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowImplicitDefiner = true });
        var text = """
        CREATE
            DEFINER=CURRENT_ROLE
        PROCEDURE my_procedure()
        SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END
        """;
        var statements = parser.ParseText(text);
        builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        var def = builder.ToDefinition();
        var proc = def.Procedures.First().Value;
        Assert.NotNull(proc);
        Assert.Equal(new Account.CurrentRole(), proc.Definer.Account);
    }

    [Fact]
    public void ImplicitDefiner_NotAllowed_CurrentUser_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowImplicitDefiner = false });
        var text = """
        CREATE
            DEFINER=CURRENT_USER
        PROCEDURE my_procedure()
        SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.ImplicitDefiner>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_procedure", exception.Message);
    }

    [Fact]
    public void ImplicitDefiner_NotAllowed_CurrentRole_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowImplicitDefiner = false });
        var text = """
        CREATE
            DEFINER=CURRENT_ROLE
        PROCEDURE my_procedure()
        SQL SECURITY DEFINER
        BEGIN
            SELECT 1;
        END
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.ImplicitDefiner>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_procedure", exception.Message);
    }

    [Fact]
    public void MissingSecurity_Allowed()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowMissingSecurityContext = true });
        var text = """
        CREATE
            DEFINER='user'@'localhost'
        PROCEDURE my_procedure()
        BEGIN
            SELECT 1;
        END
        """;
        var statements = parser.ParseText(text);
        builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        var def = builder.ToDefinition();
        var proc = def.Procedures.First().Value;
        Assert.NotNull(proc);

        Assert.NotNull(proc.SecurityContext);
        Assert.Equal(config.AttributeDefaults.ProcedureSecurityContext, proc.SecurityContext);
    }

    [Fact]
    public void MissingSecurity_NotAllowed_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowMissingSecurityContext = false });
        var text = """
        CREATE
            DEFINER='user'@'localhost'
        PROCEDURE my_procedure()
        BEGIN
            SELECT 1;
        END
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.MissingSecurityContext>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_procedure", exception.Message);
    }

    [Fact]
    public void CreateTableAsSelect_AsSelectAndNoColumnsAllowed()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowCreateTableAsSelect = true, AllowCreateTableNoColumns = true });
        var text = """
        CREATE TABLE my_table AS
        SELECT 1 AS col;
        """;

        var statements = parser.ParseText(text);
        builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        var def = builder.ToDefinition();
        var table = def.Tables.First().Value;
        Assert.NotNull(table);
        Assert.Empty(table.Columns);
    }

    [Fact]
    public void CreateTableAsSelect_AsSelectNotAllowed_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowCreateTableAsSelect = false });
        var text = """
        CREATE TABLE my_table AS
        SELECT 1 AS col;
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.CreateTableAsSelect>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_table", exception.Message);
    }

    [Fact]
    public void CreateTableAsSelect_NoColumnsNotAllowed_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowCreateTableAsSelect = true, AllowCreateTableNoColumns = false });
        var text = """
        CREATE TABLE my_table AS
        SELECT 1 AS col;
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.MissingTableColumns>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_table", exception.Message);
    }

    [Fact]
    public void CreateTableNoColumns_Allowed()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowCreateTableNoColumns = true });
        var text = """
        CREATE TABLE my_table ();
        """;

        var statements = parser.ParseText(text);
        builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        var def = builder.ToDefinition();
        var table = def.Tables.First().Value;
        Assert.NotNull(table);
        Assert.Empty(table.Columns);
    }

    [Fact]
    public void CreateTableNoColumns_NotAllowed_Throws()
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowCreateTableNoColumns = false });
        var text = """
        CREATE TABLE my_table ();
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.MissingTableColumns>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_table", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FULLTEXT ")]
    [InlineData("SPATIAL ")]
    public void UnnamedIndex_NotAllowed_Throws(string? keyType)
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowUnnamedIndex = false });
        var text = $"""
        CREATE TABLE my_table (
            id INT,
            {keyType}INDEX (id)
        );
        """;

        var statements = parser.ParseText(text);
        var exception = Assert.Throws<DefinitionException.TableConstraintNameRequired>(() => builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0));
        Assert.Contains("my_table", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FULLTEXT ")]
    [InlineData("SPATIAL ")]
    public void UnnamedIndex_Allowed(string? keyType)
    {
        var (config, builder, parser) = GetCustomDefinitionBuilder(new ValidationSettings { AllowUnnamedIndex = true });
        var text = $"""
        CREATE TABLE my_table (
            id INT,
            {keyType}INDEX (id)
        );
        """;

        var statements = parser.ParseText(text);
        builder.ProcessStatements(statements, config.Schemas.Keys.First(), 0);

        var def = builder.ToDefinition();
        var table = def.Tables.First().Value;
        Assert.NotNull(table);
        var key = Assert.Single(table.Keys);
        var handleAsGuid = key.Key.Name;
        Assert.True(Guid.TryParse(handleAsGuid, out _));
    }
}
