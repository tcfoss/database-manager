using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.Configuration;

public record SchemaMappingBase : ISchemaMapping
{
    public required SchemaIdentifier SchemaName { get; init; }
    public required string RootPath { get; init; }
    public string[] IncludeFilePatterns { get; init; } = [];
    public string[] ExcludeFilePatterns { get; init; } = [];
    public string[] ExcludeDatabaseObjectNames { get; init; } = [];
    public Refactor[] Refactors { get; init; } = [];
    public DeployScript[] DeployScripts { get; init; } = [];
}
