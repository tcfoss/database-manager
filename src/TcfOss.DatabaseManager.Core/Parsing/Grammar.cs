namespace TcfOss.DatabaseManager.Core.Parsing;

[Flags]
public enum Grammar
{
    My = 1 << 0,
    Ms = 1 << 1,
    Pg = 1 << 2
}
