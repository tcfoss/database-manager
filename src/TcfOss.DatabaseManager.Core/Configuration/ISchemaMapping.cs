using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.Configuration;

// Many attributes are accessed only via more specific type (for now), but we'll keep them on
// the interface for the sake of code using this as a library.
// ReSharper disable UnusedMemberInSuper.Global
public interface ISchemaMapping
{
    SchemaIdentifier SchemaName { get; set; }
    string RootPath { get; set; }
    string[] IncludeFilePatterns { get; set; }
    string[] ExcludeFilePatterns { get; set; }
    string[] ExcludeDatabaseObjectNames { get; set; }
    Refactor[] Refactors { get; set; }
    DeployScript[] DeployScripts { get; set; }
}
