using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class SchemaMappingBase : ISchemaMapping
{
    public required SchemaIdentifier SchemaName { get; set; }
    public required string RootPath { get; set; }
    public string[] IncludeFilePatterns { get; set; } = [];
    public string[] ExcludeFilePatterns { get; set; } = [];
    public string[] ExcludeDatabaseObjectNames { get; set; } = [];
    public Refactor[] Refactors { get; set; } = [];
    public DeployScript[] DeployScripts { get; set; } = [];
}
