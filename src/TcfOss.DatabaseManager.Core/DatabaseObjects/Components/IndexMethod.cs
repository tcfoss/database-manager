using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record IndexMethod : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public static implicit operator IndexMethod?(BuiltInIndexMethod? method)
    {
        if (method == null)
        {
            return null;
        }
        return new BuiltIn(method);
    }

    public record BuiltIn(BuiltInIndexMethod Method) : IndexMethod
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Method}");
        }
    }

    public record Custom(Identifier Method) : IndexMethod
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Method.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Method.FormatSql(writer, manager);
        }
    }

    public static IndexMethod Default => new BuiltIn(BuiltInIndexMethod.Btree);

    public static bool IsBtree(IndexMethod? method)
    {
        return method is BuiltIn builtIn && builtIn.Method == BuiltInIndexMethod.Btree;
    }
}
