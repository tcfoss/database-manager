using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
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
        public SqlValueList<PgConstraintOption>? PgOptions { get; init; }

        protected void WriteConstraintOptions(SqlTextWriter writer)
        {
            foreach (PgConstraintOption option in PgOptions ?? [])
            {
                writer.Write(" ");
                option.ToSql(writer);
            }
        }

        protected void WriteName(SqlTextWriter writer)
        {
            if (Name != null)
            {
                writer.WriteSql($"CONSTRAINT {Name} ");
            }
        }

        protected void FormatName(SqlTextWriter writer, FormatManager manager)
        {
            if (Name != null)
            {
                writer.Write("CONSTRAINT ");
                Name.FormatSql(writer, manager);
                writer.Write(" ");
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
                WriteConstraintOptions(writer);
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
            WriteConstraintOptions(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatName(writer, manager);
            writer.Write("CHECK (");
            Expression.FormatSql(writer, manager);
            writer.Write(")");
            WriteConstraintOptions(writer);
        }
    }

    public record PrimaryKey(Identifier? Name = null) : WithConstraint(Name)
    {
        public PgConstraintIndexParameters? IndexParameters { get; init; }
        public override string DuplicateCheckKey => "PRIMARY";

        public override void ToSql(SqlTextWriter writer)
        {
            WriteName(writer);
            writer.Write("PRIMARY KEY");
            if (IndexParameters != null)
            {
                writer.WriteSql($" {IndexParameters}");
            }
            WriteConstraintOptions(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatName(writer, manager);
            writer.Write("PRIMARY KEY");
            if (IndexParameters != null)
            {
                writer.Write(" ");
                IndexParameters.FormatSql(writer, manager);
            }
            WriteConstraintOptions(writer);
        }
    }

    public record Unique(Identifier? Name = null) : WithConstraint(Name)
    {
        public bool? NullsDistinct { get; init; }
        public PgConstraintIndexParameters? IndexParameters { get; init; }
        public override string DuplicateCheckKey => "UNIQUE";

        public override void ToSql(SqlTextWriter writer)
        {
            WriteName(writer);
            writer.Write("UNIQUE");
            WriteNullsDistinct(writer);
            if (IndexParameters != null)
            {
                writer.WriteSql($" {IndexParameters}");
            }
            WriteConstraintOptions(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatName(writer, manager);
            writer.Write("UNIQUE");
            WriteNullsDistinct(writer);
            if (IndexParameters != null)
            {
                writer.Write(" ");
                IndexParameters.FormatSql(writer, manager);
            }
            WriteConstraintOptions(writer);
        }

        private void WriteNullsDistinct(SqlTextWriter writer)
        {
            if (NullsDistinct != null)
            {
                writer.Write(NullsDistinct.Value ? " NULLS DISTINCT" : " NULLS NOT DISTINCT");
            }
        }
    }

    public record PgReference(ObjectName Table, SqlValueList<Identifier>? Columns = null, Identifier? Name = null) : WithConstraint(Name)
    {
        public ReferentialAction? DeleteAction { get; init; }
        public ReferentialAction? UpdateAction { get; init; }
        public override string DuplicateCheckKey => "REFERENCES";

        public override void ToSql(SqlTextWriter writer)
        {
            WriteName(writer);
            writer.WriteSql($"REFERENCES {Table}");
            if (Columns != null)
            {
                writer.WriteSql($" ({Columns})");
            }
            WriteTail(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatName(writer, manager);
            writer.Write("REFERENCES ");
            Table.FormatSql(writer, manager);
            if (Columns != null)
            {
                writer.Write(" (");
                writer.FormatDelimited(Columns, manager);
                writer.Write(")");
            }
            WriteTail(writer);
        }

        private void WriteTail(SqlTextWriter writer)
        {
            if (DeleteAction != null)
            {
                writer.WriteSql($" ON DELETE {DeleteAction}");
            }
            if (UpdateAction != null)
            {
                writer.WriteSql($" ON UPDATE {UpdateAction}");
            }
            WriteConstraintOptions(writer);
        }
    }

    public abstract record Generated() : StatementColumnOption
    {
        public override string DuplicateCheckKey => "GENERATED";

        public record PgIdentity(bool Always, SqlValueList<PgStatementSequenceOption>? Options = null) : Generated
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write(Always ? "GENERATED ALWAYS AS IDENTITY" : "GENERATED BY DEFAULT AS IDENTITY");
                if (Options != null)
                {
                    writer.WriteSql($" ({Options.ToSqlDelimited(" ")})");
                }
            }
        }

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

    public record PgCollation(ObjectName Name) : StatementColumnOption
    {
        public override string DuplicateCheckKey => "COLLATE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"COLLATE {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("COLLATE ");
            Name.FormatSql(writer, manager);
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
