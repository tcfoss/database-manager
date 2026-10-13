using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.Configuration;

// Many attributes are accessed only via more specific type (for now), but we'll keep them on
// the interface for the sake of code using this as a library.
// ReSharper disable UnusedMemberInSuper.Global
public interface ISchemaMapping
{
    SchemaIdentifier SchemaName { get; }
    string RootPath { get; }
    string[] IncludeFilePatterns { get; }
    string[] ExcludeFilePatterns { get; }
    string[] ExcludeDatabaseObjectNames { get; }
    Refactor[] Refactors { get; }
    DeployScript[] DeployScripts { get; }
}
