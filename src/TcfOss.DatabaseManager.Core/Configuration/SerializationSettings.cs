namespace TcfOss.DatabaseManager.Core.Configuration;

public class SerializationSettings
{
    public bool OmitMetaData { get; init; } = true;
    public bool OmitPreNonSql { get; init; } = true;
    public bool OmitRawText { get; init; }
    public bool OmitSourceRef { get; init; }
}
