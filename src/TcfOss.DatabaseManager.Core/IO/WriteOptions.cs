namespace TcfOss.DatabaseManager.Core.IO;

public class WriteOptions
{
    public bool PreferRawText { get; init; }
    public bool TerminateStatements { get; init; } = true;
    public bool UseDelimiterAroundPrograms { get; init; } = true;
    public bool UseSchemaOnChange { get; init; } = true;
    public bool UseCatalogOnChange { get; init; }
}
