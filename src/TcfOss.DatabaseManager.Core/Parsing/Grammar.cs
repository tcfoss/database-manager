namespace TcfOss.DatabaseManager.Core.Parsing;

[Flags]
public enum Grammar
{
    None = 0,
    My = 1 << 0,
    Ms = 1 << 1,
    Pg = 1 << 2,
    PgPl = 1 << 3,
    All = My | Ms | Pg | PgPl
}
