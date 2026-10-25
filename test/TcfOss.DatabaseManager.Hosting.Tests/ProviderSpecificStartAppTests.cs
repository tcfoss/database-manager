using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.Hosting.Tests;

public class ProviderSpecificConstructionTests
{
    [Theory]
    [InlineData("Generic")]
    [InlineData("MySql")]
    [InlineData("MsSql")]
    public void TextParser_DocumentationDirectConstruction_ParsesWithoutServices(string dialect)
    {
        var parser = dialect switch
        {
            "Generic" => new TextParser(new GenericLexer(), new Parser()),
            "MySql" => new TextParser(new MyLexer(), new MyParser()),
            "MsSql" => new TextParser(new MsLexer(), new MsParser()),
            _ => throw new ArgumentOutOfRangeException(nameof(dialect))
        };

        Assert.Single(parser.ParseText("CREATE TABLE widgets (id INT);", filename: "widgets.sql"));
    }

    [Fact]
    public void Formatter_DocumentationDirectConstruction_FormatsWithoutServices()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var startup = new GenericStartup();
        var rawConfig = startup.GetDefaultConfig(workingDirectory);
        var config = startup.BuildConfiguration(
            workingDirectory, rawConfig, new EnvironmentVariableReader(),
            relaxed: false, logger: NullLogger.Instance);

        var parser = new TextParser(new GenericLexer(), new Parser());
        var functionNames = new FunctionNameProvider();
        using var formatter = new Formatter(config, parser, functionNames);

        var formatted = formatter.GetFormatted("create table widgets(id int);");

        Assert.Contains("CREATE TABLE", formatted);
        Assert.Single(parser.ParseText(formatted));
    }

}
