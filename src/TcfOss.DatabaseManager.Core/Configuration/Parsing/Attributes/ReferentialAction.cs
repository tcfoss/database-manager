namespace TcfOss.DatabaseManager.Core.Configuration.Parsing.Attributes;

public enum ReferentialAction
{
    NotSet,
    NoAction,
    Restrict,
    Cascade,
    SetNull,
    SetDefault
}
