namespace TcfOss.DatabaseManager.Core.Configuration.Parsing.Attributes;

public enum IndexMethod
{
    NotSet,
    Btree,
    Hash,
    Rtree,
    Gist,
    SpGist,
    Gin,
    Brin
}
