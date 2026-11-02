using BenchmarkDotNet.Attributes;
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
        if (_sql.Count == 0)
        {
            throw new InvalidOperationException("The benchmark corpus contains no SQL files.");
        }

        _tokens = _sql.Select(sql => _lexer.Tokenize(sql).ToArray()).ToArray();
        Console.WriteLine($"Loaded {_sql.Count:N0} SQL files ({_sql.Sum(sql => (long)sql.Length):N0} characters).");
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

    private static List<string> LoadLinuxTestData()
    {
        string[] directories =
        [
            "/home/thomas/Code/DatabaseManager/test/Resources/TestSchemas/MySql/Library/Initial",
            "/home/thomas/Code/DatabaseManagerSupport/TestSchemas/Sakila/sakila"
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