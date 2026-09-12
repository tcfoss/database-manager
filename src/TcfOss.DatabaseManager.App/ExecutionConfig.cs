using TcfOss.DatabaseManager.Core.Configuration.Attributes;

using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.App;

public sealed class ExecutionConfig
{
    public required string WorkingDirectory { get; init; }
    public FileInfo? ConfigPath { get; init; }

    public SqlDialect? CliDialect { get; init; }
    public SqlDialect? ConfigDialect { get; init; }
    public required SqlDialect Dialect { get; init; }

    public ConfigParsing.Config? RawConfig { get; init; }
}
