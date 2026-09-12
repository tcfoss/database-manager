namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

public class SchemaMapping
{
    public required string SchemaName { get; set; }
    public required string RootPath { get; set; }
    public string[] IncludeFilePatterns { get; set; } = [];
    public string[] ExcludeFilePatterns { get; set; } = [];
    public string[] ExcludeDatabaseObjectNames { get; set; } = [];
    public string[] RefactorFiles { get; set; } = [];
    public DeployScript[] DeployScripts { get; set; } = [];
    public SchemaDefaults? SchemaDefaults { get; set; }
}
