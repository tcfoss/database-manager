namespace TcfOss.DatabaseManager.App;

public static class ExitCodes
{
    public const int Success = 0;
    public const int ParseError = 1;
    public const int SqlSyntaxError = 2;
    public const int DefinitionError = 3;
    public const int ConfigurationError = 4;
    public const int NotFoundError = 5;
    public const int OtherError = 10;
}
