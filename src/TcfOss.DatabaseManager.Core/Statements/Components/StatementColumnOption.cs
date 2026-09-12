using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using GenerationMode = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.GenerationMode;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementColumnOption() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract string DuplicateCheckKey { get; }
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public abstract record WithConstraint(Identifier? Name) : StatementColumnOption
    {
        protected void WriteName(SqlTextWriter writer)
        {
            if (Name != null)
            {
                writer.WriteSql($"CONSTRAINT {Name} ");
            }
        }
    }

    public abstract record Nullability(Identifier? Name) : WithConstraint(Name)
    {
        public override string DuplicateCheckKey => "NULLABILITY";

        public record Null(Identifier? Name = null) : Nullability(Name)
        {
            public override void ToSql(SqlTextWriter writer)
            {
                WriteName(writer);
                writer.Write("NULL");
            }
        }

        public record NotNull(Identifier? Name = null) : Nullability(Name)
        {
            public override void ToSql(SqlTextWriter writer)
            {
                WriteName(writer);
                writer.Write("NOT NULL");
            }
        }
    }

    public abstract record Default(Identifier? Name) : WithConstraint(Name)
    {
        public override string DuplicateCheckKey => "DEFAULT";

        public record DefaultValue(Value Value, Identifier? Name = null) : Default(Name)
        {
            public override void ToSql(SqlTextWriter writer)
            {
                WriteName(writer);
                writer.WriteSql($"DEFAULT {Value}");
            }
        }

        public record DefaultExpression(Expression Expression, Identifier? Name = null) : Default(Name)
        {
            public override void ToSql(SqlTextWriter writer)
            {
                WriteName(writer);
                writer.WriteSql($"DEFAULT ({Expression})");
            }
        }
    }

    public record OnUpdate(Expression Expression) : StatementColumnOption
    {
        public override string DuplicateCheckKey => "ONUPDATE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON UPDATE {Expression}");
        }
    }

    public record CheckConstraint(Expression Expression, Identifier? Name = null) : WithConstraint(Name)
    {
        public override string DuplicateCheckKey => "CHECK";

        public override void ToSql(SqlTextWriter writer)
        {
            WriteName(writer);
            writer.WriteSql($"CHECK ({Expression})");
        }
    }

    public record PrimaryKey(Identifier? Name = null) : WithConstraint(Name)
    {
        public override string DuplicateCheckKey => "PRIMARY";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("PRIMARY KEY");
        }
    }

    public record Unique(Identifier? Name = null) : WithConstraint(Name)
    {
        public override string DuplicateCheckKey => "UNIQUE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("UNIQUE");
        }
    }

    public abstract record Generated() : StatementColumnOption
    {
        public override string DuplicateCheckKey => "GENERATED";

        public record AsExpression(Expression Expression, GenerationMode Mode, bool UseGeneratedAlways, bool ModeGiven, bool StoredAsPersistent) : Generated
        {
            public override void ToSql(SqlTextWriter writer)
            {
                string initial = UseGeneratedAlways ? "GENERATED ALWAYS AS" : "AS";
                writer.WriteSql($"{initial} ({Expression})");
                if (ModeGiven)
                {
                    if (Mode == GenerationMode.Stored && StoredAsPersistent)
                    {
                        writer.Write(" PERSISTENT");
                    }
                    else
                    {
                        writer.WriteSql($" {Mode}");
                    }
                }
            }
        }
    }

    public record ColumnComment(Comment Comment) : StatementColumnOption
    {
        public override string DuplicateCheckKey => "COMMENT";

        public override void ToSql(SqlTextWriter writer)
        {
            Comment.ToSql(writer);
        }
    }

    public record AutoIncrement() : StatementColumnOption
    {
        public override string DuplicateCheckKey => "AUTO_INCREMENT";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("AUTO_INCREMENT");
        }
    }

    public record Identity() : StatementColumnOption
    {
        public override string DuplicateCheckKey => "IDENTITY";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("IDENTITY");
        }

        public record Specified(ulong Seed, ulong Increment) : Identity
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.WriteSql($"IDENTITY({Seed}, {Increment})");
            }
        }
    }
}
