using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Statements.Components;

public abstract record MyStatementTableOption() : StatementTableOption
{
    public abstract record MyWithOptionalEquals() : MyStatementTableOption
    {
        public bool IncludeEquals { get; init; }
        protected string EqualSign => IncludeEquals ? " = " : " ";
    }

    public record Engine(string Value) : MyWithOptionalEquals
    {
        public override string DuplicateCheckKey => "ENGINE";

        public bool IncludeStorage { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            string? storage = IncludeStorage ? "STORAGE " : null;

            writer.WriteSql($"{storage}ENGINE{EqualSign}{Value}");

        }
    }

    public record CharacterSet(string Value) : MyWithOptionalEquals
    {
        public override string DuplicateCheckKey => "CHARSET";

        public bool IncludeDefault { get; init; }
        public bool AsCharset { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            string? includeDefault = IncludeDefault ? "DEFAULT " : null;
            string label = AsCharset ? "CHARSET" : "CHARACTER SET";
            writer.WriteSql($"{includeDefault}{label}{EqualSign}{Value}");
        }
    }

    public record Collation(string Value) : MyWithOptionalEquals
    {
        public override string DuplicateCheckKey => "COLLATION";

        public bool IncludeDefault { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            string? includeDefault = IncludeDefault ? "DEFAULT " : null;
            writer.WriteSql($"{includeDefault}COLLATE{EqualSign}{Value}");
        }
    }

    public record AutoIncrement(ulong Value) : MyWithOptionalEquals
    {
        public override string DuplicateCheckKey => "AUTO_INCREMENT";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"AUTO_INCREMENT{EqualSign}{Value}");
        }
    }

    public record Comment(string Value) : MyWithOptionalEquals
    {
        public override string DuplicateCheckKey => "COMMENT";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"COMMENT{EqualSign}'{Value}'");
        }
    }
}
