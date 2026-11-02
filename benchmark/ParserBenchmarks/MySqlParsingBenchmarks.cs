using BenchmarkDotNet.Attributes;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace ParserBenchmarks;

[MemoryDiagnoser]
public class MySqlParsingBenchmarks
{
    private readonly MyLexer _lexer = new();
    private readonly MyParser _parser = new();
    private List<string> _sql = null!;
    private Token[][] _tokens = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sql = LoadLinuxTestData();
        _sql.AddRange(LoadQuotedIdentifierTestData());
        _sql.AddRange(LoadEscapedStringTestData());
        if (_sql.Count == 0)
        {
            throw new InvalidOperationException("The benchmark corpus contains no SQL files.");
        }

        _tokens = _sql.Select(sql => _lexer.Tokenize(sql).ToArray()).ToArray();
        Console.WriteLine($"Loaded {_sql.Count:N0} SQL inputs ({_sql.Sum(sql => (long)sql.Length):N0} characters).");
        Word[] quotedIdentifiers = _tokens.SelectMany(tokens => tokens).OfType<Word>()
            .Where(word => word.QuoteStyle != QuoteStyle.None).ToArray();
        Console.WriteLine($"Quoted identifiers: {string.Join(", ", quotedIdentifiers.GroupBy(word => word.QuoteStyle).Select(group => $"{group.Key}={group.Count()}"))}.");
        int doubledQuotes = quotedIdentifiers.Count(word => word.Value.Contains(word.QuoteStyle switch
        {
            QuoteStyle.Ansi => "\"\"",
            QuoteStyle.Brackets => "]]",
            _ => "``"
        }, StringComparison.Ordinal));
        Console.WriteLine($"Quoted identifier cases: doubled={doubledQuotes}, empty={quotedIdentifiers.Count(word => word.Value.Length == 0)}, multiline={quotedIdentifiers.Count(word => word.Value.AsSpan().IndexOfAny('\r', '\n') >= 0)}, longer than 16={quotedIdentifiers.Count(word => word.Value.Length > 16)}, labels={_tokens.SelectMany(tokens => tokens).OfType<Label>().Count()}.");
        string[] strings = _tokens.SelectMany(tokens => tokens).Select(token => token switch
        {
            StringLiteral literal => literal.Value,
            NationalStringLiteral literal => literal.Value,
            _ => null
        }).OfType<string>().ToArray();
        Console.WriteLine($"String literal cases: total={strings.Length}, doubled={strings.Count(value => value.Contains("''", StringComparison.Ordinal))}, backslashes={strings.Count(value => value.Contains('\\'))}, empty={strings.Count(value => value.Length == 0)}, multiline={strings.Count(value => value.AsSpan().IndexOfAny('\r', '\n') >= 0)}, longer than 256={strings.Count(value => value.Length > 256)}.");
    }

    [Benchmark]
    public int EndToEnd()
    {
        int statementCount = 0;
        foreach (string sql in _sql)
        {
            statementCount += _parser.Parse(_lexer.Tokenize(sql).ToArray()).Count;
        }

        return statementCount;
    }

    [Benchmark]
    public int LexerOnly()
    {
        int tokenCount = 0;
        foreach (string sql in _sql)
        {
            tokenCount += _lexer.Tokenize(sql).Count;
        }

        return tokenCount;
    }

    [Benchmark]
    public int ParserOnly()
    {
        int statementCount = 0;
        foreach (Token[] tokens in _tokens)
        {
            statementCount += _parser.Parse(tokens).Count;
        }

        return statementCount;
    }

    private static List<string> LoadQuotedIdentifierTestData()
    {
        List<string> sql = [];
        foreach ((string open, string close) in new (string, string)[] { ("`", "`"), ("\"", "\""), ("[", "]") })
        {
            string[] names =
            [
                "plain",
                "",
                $"escaped{close}{close}name",
                $"trailing{close}{close}",
                "first\nsecond",
                "first\r\nsecond",
                "caf\u00e9",
                new string('a', 64)
            ];
            sql.Add($"SELECT {string.Join(", ", names.Select(name => $"{open}{name}{close}"))};");
            string label = $"{open}label{close}{close}name{close}";
            sql.Add($"CREATE PROCEDURE quoted_identifier_probe() BEGIN {label}: BEGIN SELECT 1; END; END;");
        }
        return sql;
    }

    private static List<string> LoadEscapedStringTestData()
    {
        string[][] groups =
        [
            [
                "",
                "plain",
                "caf\u00e9",
                "''",
                "''''",
                "trailing''",
                "before\\'after",
                "\\n",
                new string('\\', 2) + "'tail",
                new string('\\', 3) + "'tail",
                new string('\\', 4) + "'tail",
                "before''\nafter\\'tail",
                "line\r\nbreak"
            ],
            [
                new string('a', 252) + "''",
                new string('a', 253) + "\\'",
                new string('a', 254) + "''",
                new string('a', 255) + "\\'"
            ],
            [
                new string('a', 224) + "''" + new string('b', 32),
                new string('a', 225) + "\\'" + new string('b', 32),
                new string('a', 300) + "''" + new string('b', 1024),
                "\\'" + new string('b', 1024)
            ],
            [
                new string('a', 1024),
                new string('a', 4096),
                new string('a', 512) + "\n" + new string('b', 512),
                new string('a', 512) + "\r\n" + new string('b', 512)
            ]
        ];
        List<string> sql = [];
        foreach (string prefix in new[] { "", "N" })
        {
            foreach (string[] contents in groups)
            {
                sql.Add($"SELECT {string.Join(", ", contents.Select(content => $"{prefix}'{content}'"))};");
            }
        }
        return sql;
    }

    private static List<string> LoadLinuxTestData()
    {
        string[] directories =
        [
            "/home/thomas/Code/DatabaseManager/test/Resources/TestSchemas/MySql/Library/Initial",
            "/home/thomas/Code/DatabaseManagerSupport/TestSchemas/Sakila/sakila",
            "/home/thomas/Code/IssueTracker/database"
        ];

        List<string> sql = [];
        foreach (string directory in directories)
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal))
            {
                sql.Add(File.ReadAllText(path));
            }
        }

        return sql;
    }
}