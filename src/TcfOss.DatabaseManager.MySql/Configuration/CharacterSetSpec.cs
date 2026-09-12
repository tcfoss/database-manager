namespace TcfOss.DatabaseManager.MySql.Configuration;

public class CharacterSetSpec
{
    public required string CharacterSet { get; init; }
    public required string DefaultCollation { get; init; }
    public required HashSet<string> Collations { get; init; }
}
