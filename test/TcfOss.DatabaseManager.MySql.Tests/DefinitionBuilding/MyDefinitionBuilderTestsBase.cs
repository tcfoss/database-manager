using System.Globalization;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding.MyDefinitionBuilderTestsBase.DataTypeSerializer), typeof(DataType))]

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;

public abstract class MyDefinitionBuilderTestsBase
{
    protected abstract bool Relaxed { get; }
    protected abstract MyConfig MyConfig { get; }
    protected abstract MyDefinitionBuilder DefinitionBuilder { get; }
    protected static TextParser TextParser => new(new MyLexer(), new MyParser());
    protected virtual string DefaultCollation => "utf8mb4_0900_ai_ci";
    protected string DefaultCollationSetter => $" COLLATE {DefaultCollation}";
    protected virtual string DefaultIntegerWidth => "";
    protected virtual string DefaultUnsignedIntegerWidth => "";
    protected virtual RoutineParameterDirection? FunctionParameterDirection => null;
    protected SchemaIdentifier Schema => MyConfig.Schemas.Keys.First();
    private bool AllowCheckOnColumn => MyConfig.Dialect == SqlDialect.MariaDb;

    protected abstract MyDefinitionBuilder GetDefinitionBuilder();

    protected DifferFormatManager GetDifferFormatManager()
    {
        return new DifferFormatManager()
        {
            QuoteStyle = MyConfig.QuoteStyle,
            Formatting = MyConfig.DifferFormatting,
            AttributeDefaults = MyConfig.AttributeDefaults,
        };
    }

    [Fact]
    public void Add_One_Table_Succeeds()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 {DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Tables);
        Assert.Contains(def.Tables.Values, t => t.Name is { Name: "table1", Schema.Name: "schema1" });
    }

    [Fact]
    public void Defaults_Applied_To_Table()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL
        );
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Tables);
        var table = def.Tables.Values.First();
        Assert.Equal("table1", table.Name.Name);
        Assert.Equal("schema1", table.Name.Schema.Name);
        Assert.Equal("InnoDB", table.Engine);
        Assert.Equal("utf8mb4", table.CharacterSet);
        Assert.Equal(DefaultCollation, table.Collation);
    }


    [Fact]
    public void Add_Two_Tables_Succeeds()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE `uc_table1` (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            parent_id INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NOT NULL,
            `location` POINT NOT NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_parent` (parent_id),
            SPATIAL INDEX `sp_idx_location` (location),
            CONSTRAINT `uc_table2` UNIQUE KEY USING BTREE (`val`) COMMENT = 'Unique constraint on val',
            CONSTRAINT `fk_table2_parent` FOREIGN KEY (parent_id) REFERENCES `schema1`.`table1`(id) COMMENT 'Foreign key to table1'
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Equal(2, def.Tables.Count);

        var table1Obj = def.Tables.Values.FirstOrDefault(t => t.Name is { Name: "table1", Schema.Name: "schema1" });
        Assert.NotNull(table1Obj);
        var table1 = table1Obj.ToCreateStatement(true, GetDifferFormatManager());

        var uc1 = table1.Constraints.Where(c => c is StatementTableConstraint.UniqueConstraint).OfType<StatementTableConstraint.UniqueConstraint>().FirstOrDefault();
        Assert.NotNull(uc1);
        Assert.Equal("CONSTRAINT `uc_table1` UNIQUE KEY USING BTREE (`val` ASC)", uc1.ToSql());

        var table2Obj = def.Tables.Values.FirstOrDefault(t => t.Name is { Name: "table2", Schema.Name: "schema1" });
        Assert.NotNull(table2Obj);
        var table2 = table2Obj.ToCreateStatement(true, GetDifferFormatManager());
        var uc2 = table2.Constraints.Where(c => c is StatementTableConstraint.UniqueConstraint).OfType<StatementTableConstraint.UniqueConstraint>().FirstOrDefault();
        Assert.NotNull(uc2);
        Assert.Equal("CONSTRAINT `uc_table2` UNIQUE KEY USING BTREE (`val` ASC) COMMENT = 'Unique constraint on val'", uc2.ToSql());

        var fk = table2.Constraints.Where(c => c is StatementTableConstraint.ForeignKey).OfType<StatementTableConstraint.ForeignKey>().FirstOrDefault();
        Assert.NotNull(fk);
        Assert.Equal($"CONSTRAINT `fk_table2_parent` FOREIGN KEY (`parent_id`) REFERENCES `table1` (`id`) ON DELETE {MyConfig.AttributeDefaults.ForeignKeyOnDelete} ON UPDATE {MyConfig.AttributeDefaults.ForeignKeyOnUpdate} COMMENT 'Foreign key to table1'", fk.ToSql());

        var sp = table2.Constraints.Where(i => i is StatementTableConstraint.Spatial).OfType<StatementTableConstraint.Spatial>().FirstOrDefault();
        Assert.NotNull(sp);
        Assert.Equal("SPATIAL KEY `sp_idx_location` (`location`)", sp.ToSql());
    }

    [Fact]
    public void PrimaryKey_MissingColumn_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`fake_column`),
            CONSTRAINT `uc_table1` UNIQUE (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);
        var ex = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        Assert.IsType<SqlSyntaxException.IndexColumnNotFound>(ex.Exceptions.First());
    }

    [Fact]
    public void Validate_AutoIncrement_NoPrimaryKey_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
            `val` VARCHAR(100) NULL,
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.AutoIncrementPrimaryKeyOnly>(ex);
    }

    [Fact]
    public void Validate_AutoIncrement_DifferentColumnPrimaryKey_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.AutoIncrementPrimaryKeyOnly>(ex);
    }

    [Fact]
    public void Validate_AutoIncrement_NotInteger_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` VARCHAR(100) NOT NULL AUTO_INCREMENT,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.AutoIncrementIntegerOnly>(ex);
    }

    [Fact]
    public void UniqueKey_OnColumn_Throws()
    {
        var createStatement = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL UNIQUE
        );
        """;

        var statements = TextParser.ParseText(createStatement);

        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            var uk = def.Tables.Values.First().Columns.First(c => c.Name.Name == "first_name").Unique;

            Assert.NotNull(uk);
        }
        else
        {
            Assert.Throws<DefinitionException.UniqueColumn>(() => DefinitionBuilder.ProcessStatements(statements, Schema, 0));
        }
    }

    [Fact]
    public void UniqueKey_OnColumn_Named_Throws()
    {
        var createStatement = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL CONSTRAINT myconstraint UNIQUE
        );
        """;

        var statements = TextParser.ParseText(createStatement);

        if (Relaxed)
        {
            Assert.Throws<SqlSyntaxException.ConstraintNameNotAllowedException>(() => DefinitionBuilder.ProcessStatements(statements, Schema, 0));
        }
        else
        {
            Assert.Throws<DefinitionException.UniqueColumn>(() => DefinitionBuilder.ProcessStatements(statements, Schema, 0));
        }
    }

    [Fact]
    public void UniqueKey_MissingColumn_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE (`fake_val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);
        var ex = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        Assert.IsType<SqlSyntaxException.IndexColumnNotFound>(ex.Exceptions.First());
    }

    [Fact]
    public void ForeignKey_Correct_Succeeds()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            parent_id INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NOT NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_parent` (parent_id),
            CONSTRAINT `fk_table2_parent` FOREIGN KEY (parent_id) REFERENCES `schema1`.`table1`(id)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);
        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Equal(2, def.Tables.Count);
        Assert.Contains(def.Tables.Values, t => t.Name is { Name: "table1", Schema.Name: "schema1" });
        Assert.Contains(def.Tables.Values, t => t.Name is { Name: "table2", Schema.Name: "schema1" });

        var table2 = def.Tables.Values.First(t => t.Name is { Name: "table2", Schema.Name: "schema1" });
        Assert.Single(table2.ForeignKeys);
        var fk = table2.ForeignKeys.Values.First();
        Assert.NotNull(fk.Name);
        Assert.Equal("fk_table2_parent", fk.Name.Name);
        Assert.Equal("table1", fk.ReferencedTable.Name);
        Assert.Equal("schema1", fk.ReferencedTable.Schema.Name);
        Assert.Single(fk.Columns);
        Assert.Contains(fk.Columns, c => c.Name == "parent_id");
        Assert.Single(fk.ReferencedColumns);
        Assert.Contains(fk.ReferencedColumns, c => c.Name == "id");
    }

    [Fact]
    public void ForeignKey_LocalColumnMissing_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`missing_column`) REFERENCES `table1`(`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        if (Relaxed)
        {
            var ex = Assert.Single(errors.Exceptions);
            Assert.IsType<SqlSyntaxException.IndexColumnNotFound>(ex);
        }
        else
        {
            Assert.Equal(2, errors.Exceptions.Count);
            Assert.Contains(errors.Exceptions, ex => ex is SqlSyntaxException.IndexColumnNotFound);
            Assert.Contains(errors.Exceptions, ex => ex is DefinitionException.ForeignKeyNoBackingIndex);
        }
    }

    [Fact]
    public void ForeignKey_ReferencedTableMissing_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_val` (`val`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`val`) REFERENCES `table1`(`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.ForeignKeyReferencedTableNotFound>(ex);
    }

    [Fact]
    public void ForeignKey_ReferencedColumnMissing_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_val` (`val`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`val`) REFERENCES `table1`(`missing_column`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        if (Relaxed)
        {
            var ex = Assert.Single(errors.Exceptions);
            Assert.IsType<SqlSyntaxException.ForeignKeyReferencedColumnNotFound>(ex);
        }
        else
        {
            Assert.Equal(2, errors.Exceptions.Count);
            Assert.Contains(errors.Exceptions, ex => ex is SqlSyntaxException.ForeignKeyReferencedColumnNotFound);
            Assert.Contains(errors.Exceptions, ex => ex is DefinitionException.ForeignKeyNoReferencedBackingIndex);
        }
    }

    [Fact]
    public void ForeignKey_ColumnLengthMismatch_1_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table1_id_val` (`id`, `val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_val` (`val`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`val`) REFERENCES `table1`(`id`, `val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;

        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.ForeignKeyColumnCountMismatch>(ex);
    }

    [Fact]
    public void ForeignKey_ColumnLengthMismatch_2_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table1_val` (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_id_val` (`id`,`val`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`id`, `val`) REFERENCES `table1`(`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;

        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.ForeignKeyColumnCountMismatch>(ex);
    }

    [Fact]
    public void ForeignKey_MissingBackingIndex_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            parent_id INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NOT NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `fk_table2_parent` FOREIGN KEY (parent_id) REFERENCES `schema1`.`table1`(id)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        if (Relaxed)
        {
            var def = DefinitionBuilder.ToDefinition();
            var fk = def.Tables.Values.First(t => t.Name is { Name: "table2", Schema.Name: "schema1" }).ForeignKeys.Values.FirstOrDefault();
            Assert.NotNull(fk);
            Assert.NotNull(fk.Name);
            Assert.Equal("fk_table2_parent", fk.Name.Name);
            Assert.Equal("table1", fk.ReferencedTable.Name);
            Assert.Equal("schema1", fk.ReferencedTable.Schema.Name);
            Assert.Single(fk.Columns);
            Assert.Contains(fk.Columns, c => c.Name == "parent_id");
        }
        else
        {
            var ex = Assert.Throws<ValidationExceptionSet>(() =>
            {
                DefinitionBuilder.ToDefinition();
            });

            Assert.IsType<DefinitionException.ForeignKeyNoBackingIndex>(ex.Exceptions.First());
        }
    }

    [Fact]
    public void ForeignKey_MissingReferencedBackingIndex_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_val` (`val`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`val`) REFERENCES `table1`(`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;

        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        if (Relaxed)
        {
            var def = DefinitionBuilder.ToDefinition();
            var fk = def.Tables.Values.First(t => t.Name.Name == "table2").ForeignKeys.Values.FirstOrDefault();
            Assert.NotNull(fk);
            Assert.NotNull(fk.Name);
            Assert.Equal("fk_table2_table1", fk.Name.Name);
            var col = Assert.Single(fk.Columns);
            Assert.Equal("val", col.Name);
        }
        else
        {
            var errors = Assert.Throws<ValidationExceptionSet>(() =>
            {
                DefinitionBuilder.ToDefinition();
            });

            var ex = Assert.Single(errors.Exceptions);
            Assert.IsType<DefinitionException.ForeignKeyNoReferencedBackingIndex>(ex);
        }
    }

    [Fact]
    public void ForeignKey_MissingBackingIndex_Multicol_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`, `val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`id`, `val`) REFERENCES `table1`(`id`, `val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        if (Relaxed)
        {
            var def = DefinitionBuilder.ToDefinition();
            var fk = def.Tables.Values.First(t => t.Name.Name == "table2").ForeignKeys.Values.FirstOrDefault();
            Assert.NotNull(fk);
            Assert.NotNull(fk.Name);
            Assert.Equal("fk_table2_table1", fk.Name.Name);
            Assert.Equal(2, fk.Columns.Count);
            Assert.Equal("id", fk.Columns[0].Name);
            Assert.Equal("val", fk.Columns[1].Name);
            Assert.Equal(2, fk.ReferencedColumns.Count);
            Assert.Equal("id", fk.ReferencedColumns[0].Name);
            Assert.Equal("val", fk.ReferencedColumns[1].Name);
        }
        else
        {
            var errors = Assert.Throws<ValidationExceptionSet>(() =>
            {
                DefinitionBuilder.ToDefinition();
            });

            var ex = Assert.Single(errors.Exceptions);
            Assert.IsType<DefinitionException.ForeignKeyNoBackingIndex>(ex);
        }
    }

    [Fact]
    public void ForeignKey_DataTypeMismatch_Throws()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table1_val` (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            `val` INT NOT NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_val` (`val`),
            CONSTRAINT `fk_table2_table1` FOREIGN KEY (`val`) REFERENCES `table1`(`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;

        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.ForeignKeyColumnTypeMismatch>(ex);
    }

    [Fact]
    public void View()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE (`val`)
        );

        CREATE TABLE `schema1`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            parent_id INT UNSIGNED NOT NULL,
            `val` DECIMAL(10, 2) NOT NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_parent` (parent_id),
            CONSTRAINT `fk_table2_parent` FOREIGN KEY (parent_id) REFERENCES `schema1`.`table1`(id)
        );

        CREATE
            ALGORITHM = UNDEFINED
            DEFINER = `root`@`localhost`
            SQL SECURITY DEFINER
        VIEW `schema1`.`myview`
        AS
            SELECT
                t1.id,
                t1.val,
                t2.val AS table2_val
            FROM table1 AS t1
            JOIN table2 AS t2
                ON t1.id = t2.parent_id;
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Views);

        var view = def.Views.FirstOrDefault(v => v.Key.Name == "myview").Value;
        Assert.Equal("myview", view.Name.Name);
        Assert.Equal("schema1", view.Name.Schema.Name);

        var actual = (view.NormalizedBody ?? view.Body).ToSql();
        var expected = "SELECT `t1`.`id` AS `id`, `t1`.`val` AS `val`, `t2`.`val` AS `table2_val` FROM `schema1`.`table1` AS `t1` INNER JOIN `schema1`.`table2` AS `t2` ON `t1`.`id` = `t2`.`parent_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void View_Everything_Gets_Qualified()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`),
            CONSTRAINT `uc_table1` UNIQUE (`val`)
        );

        CREATE TABLE `schema2`.`table2` (
            `id` INT UNSIGNED NOT NULL,
            parent_id INT UNSIGNED NOT NULL,
            `val` DECIMAL(10, 2) NOT NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_table2_parent` (parent_id),
            CONSTRAINT `fk_table2_parent` FOREIGN KEY (parent_id) REFERENCES `schema1`.`table1`(id)
        );

        USE schema1;

        CREATE
            ALGORITHM = UNDEFINED
            DEFINER = `root`@`localhost`
            SQL SECURITY DEFINER
        VIEW `schema1`.`myview`
        AS
            SELECT
                table1.id,
                table1.val,
                table2.val AS table2_val
            FROM table1
            JOIN table2
                ON table1.id = table2.parent_id;
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Views);

        var view = def.Views.FirstOrDefault(v => v.Key.Name == "myview").Value;
        Assert.Equal("myview", view.Name.Name);
        Assert.Equal("schema1", view.Name.Schema.Name);

        var expected = "SELECT `schema1`.`table1`.`id` AS `id`, `schema1`.`table1`.`val` AS `val`, `schema2`.`table2`.`val` AS `table2_val` FROM `schema1`.`table1` INNER JOIN `schema2`.`table2` ON `schema1`.`table1`.`id` = `schema2`.`table2`.`parent_id`";
        var actual = (view.NormalizedBody ?? view.Body).ToSql();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Function_Simple_Return()
    {
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        FUNCTION `schema1`.`my_function`(param1 INT, param2 VARCHAR(100))
            RETURNS VARCHAR(255)
            DETERMINISTIC
            SQL SECURITY DEFINER
            COMMENT 'This is a test function'
        RETURN CONCAT(param1, param2);
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Functions);

        var function = def.Functions.FirstOrDefault(f => f.Key.Name == "my_function").Value;
        Assert.Equal("my_function", function.Name.Name);
        Assert.Equal("schema1", function.Name.Schema.Name);
        Assert.Equal("VARCHAR(255)", function.ReturnType.ToSql());

        Assert.NotNull(function.Comment);
        Assert.Equal("This is a test function", function.Comment.Text);

        var parameters = function.Parameters.ToList();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("param1", parameters[0].Name.Name);
        Assert.Equal($"INT{DefaultIntegerWidth} SIGNED", parameters[0].DataType.ToSql());
        Assert.Equal(FunctionParameterDirection, Assert.IsType<RoutineParameter.Directed>(parameters[0]).Direction);
        Assert.Equal("param2", parameters[1].Name.Name);
        Assert.Equal($"VARCHAR(100) CHARACTER SET utf8mb4{DefaultCollationSetter}", parameters[1].DataType.ToSql());
        Assert.Equal(FunctionParameterDirection, Assert.IsType<RoutineParameter.Directed>(parameters[1]).Direction);

        Assert.True(function.Deterministic);

        Assert.Equal(new Account.IdentityWithHost(new ExtendedIdentifier("root", ExtendedQuoteStyle.Backticks), new ExtendedIdentifier("localhost", ExtendedQuoteStyle.Backticks)), function.Definer.Account);

        Assert.Equal(SqlDataRelation.ContainsSql, function.SqlDataRelation);

        Assert.Equal("RETURN CONCAT(param1, param2)", function.Body.ToSql());
    }

    [Fact]
    public void Function_Begin_End()
    {
        var statementText = """
        CREATE
            DEFINER = `admin_users`
        FUNCTION `schema1`.`my_function`(INOUT param1 INT, OUT param2 VARCHAR(100))
            RETURNS VARCHAR(255)
            NOT DETERMINISTIC
            SQL SECURITY INVOKER
            READS SQL DATA
        BEGIN
            SET param2 = 'success';
            RETURN CONCAT(param1, ' is a parameter');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Functions);

        var function = def.Functions.FirstOrDefault(f => f.Key.Name == "my_function").Value;
        Assert.Equal("my_function", function.Name.Name);
        Assert.Equal("schema1", function.Name.Schema.Name);
        Assert.Equal("VARCHAR(255)", function.ReturnType.ToSql());

        var parameters = function.Parameters.ToList();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("param1", parameters[0].Name.Name);
        Assert.Equal($"INT{DefaultIntegerWidth} SIGNED", parameters[0].DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.InOut, Assert.IsType<RoutineParameter.Directed>(parameters[0]).Direction);
        Assert.Equal("param2", parameters[1].Name.Name);
        Assert.Equal($"VARCHAR(100) CHARACTER SET utf8mb4{DefaultCollationSetter}", parameters[1].DataType.ToSql());
        Assert.Equal(RoutineParameterDirection.Out, Assert.IsType<RoutineParameter.Directed>(parameters[1]).Direction);

        Assert.Equal(SqlDataRelation.ReadsSqlData, function.SqlDataRelation);

        Assert.False(function.Deterministic);
        Assert.Equal(new Account.Identity(new ExtendedIdentifier("admin_users", ExtendedQuoteStyle.Backticks)), function.Definer.Account);

        Assert.Equal("BEGIN SET param2 = 'success'; RETURN CONCAT(param1, ' is a parameter'); END", function.Body.ToSql());
    }

    [Fact]
    public void Procedure()
    {
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        PROCEDURE `schema1`.`my_procedure`(IN param1 INT, OUT param2 VARCHAR(100))
            DETERMINISTIC
            SQL SECURITY DEFINER
            COMMENT 'This is a test procedure'
        BEGIN
            SET param2 = CONCAT(param1, ' is the input');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Procedures);

        var procedure = def.Procedures.FirstOrDefault(p => p.Key.Name == "my_procedure").Value;
        Assert.Equal("my_procedure", procedure.Name.Name);
        Assert.Equal("schema1", procedure.Name.Schema.Name);

        Assert.True(procedure.Deterministic);
        Assert.Equal(new Account.IdentityWithHost(new ExtendedIdentifier("root", ExtendedQuoteStyle.Backticks), new ExtendedIdentifier("localhost", ExtendedQuoteStyle.Backticks)), procedure.Definer.Account);

        Assert.NotNull(procedure.Comment);
        Assert.Equal("This is a test procedure", procedure.Comment.Text);

        Assert.Equal("BEGIN SET param2 = CONCAT(param1, ' is the input'); END", procedure.Body.ToSql());
    }

    [Fact]
    public void Trigger_Simple()
    {
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Triggers);

        var trigger = def.Triggers.FirstOrDefault(t => t.Key.Name == "my_trigger").Value;
        Assert.Equal("my_trigger", trigger.Name.Name);
        Assert.Equal("schema1", trigger.Name.Schema.Name);

        Assert.Equal("table1", trigger.OnTable.Name);
        Assert.Equal(TriggerEvent.Insert, trigger.TriggerEvent);
        Assert.Equal(TriggerTime.Before, trigger.TriggerTime);

        Assert.Equal("BEGIN SET NEW.val = CONCAT(NEW.val, ' - modified'); END", trigger.Body.ToSql());
    }

    [Fact]
    public void Two_Triggers_Same_Table_Event()
    {
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger1`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 1');
        END;

        CREATE
            DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger2`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
            FOLLOWS `my_trigger1`
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 2');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Equal(2, def.Triggers.Count);

        var trigger1 = def.Triggers.FirstOrDefault(t => t.Key.Name == "my_trigger1").Value;
        Assert.NotNull(trigger1);
        Assert.Equal("my_trigger1", trigger1.Name.Name);
        Assert.Equal("schema1", trigger1.Name.Schema.Name);
        Assert.Equal("table1", trigger1.OnTable.Name);
        Assert.Equal(TriggerEvent.Insert, trigger1.TriggerEvent);
        Assert.Equal(TriggerTime.Before, trigger1.TriggerTime);
        Assert.Equal<uint>(1, trigger1.Order);

        var trigger2 = def.Triggers.FirstOrDefault(t => t.Key.Name == "my_trigger2").Value;
        Assert.NotNull(trigger2);
        Assert.Equal("my_trigger2", trigger2.Name.Name);
        Assert.Equal("schema1", trigger2.Name.Schema.Name);
        Assert.Equal("table1", trigger2.OnTable.Name);
        Assert.Equal(TriggerEvent.Insert, trigger2.TriggerEvent);
        Assert.Equal(TriggerTime.Before, trigger2.TriggerTime);
        Assert.Equal<uint>(2, trigger2.Order);

        Assert.Equal("BEGIN SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 2'); END", trigger2.Body.ToSql());
    }

    [Fact]
    public void Trigger_MultipleEvents_Throws()
    {
        // MySQL/MariaDB only allow one event per trigger; parse a valid single-event
        // statement, then synthesize a two-event variant to exercise the guard.
        var statementText = """
        CREATE
            DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.`val` = NEW.`val`;
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        var original = statements.OfType<CreateTrigger>().Single();
        var multiEvent = original with { Events = [TriggerEvent.Insert, TriggerEvent.Update] };

        Assert.Throws<SqlSyntaxException.TriggerSupportsOnlyOneEvent>(
            () => DefinitionBuilder.ProcessStatements([multiEvent], Schema, 0));
    }

    [Fact]
    public void Event_Every()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE
            DEFINER = `root`@`localhost`
        EVENT `schema1`.`my_event`
            ON SCHEDULE EVERY 1 DAY
            STARTS '2023-01-01 00:00:00'
            ENDS '2035-01-01 00:00:00'
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Events);

        var evt = def.Events.FirstOrDefault(e => e.Key.Name == "my_event").Value;
        Assert.Equal("my_event", evt.Name.Name);
        Assert.Equal("schema1", evt.Name.Schema.Name);

        Assert.Equal("ON SCHEDULE EVERY 1 DAY STARTS '2023-01-01 00:00:00' ENDS '2035-01-01 00:00:00'", evt.Schedule.ToSql());
        Assert.Equal("ENABLE", evt.EventEnabledStatus.ToSql());
        Assert.False(evt.OnCompletionPreserve);
        Assert.Equal("UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event')", evt.Body.ToSql());
    }

    [Fact]
    public void Event_At()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE
            DEFINER = `root`@`localhost`
        EVENT `schema1`.`my_event`
            ON SCHEDULE AT '2023-01-01 00:00:00'
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Single(def.Events);

        var evt = def.Events.FirstOrDefault(e => e.Key.Name == "my_event").Value;
        Assert.Equal("my_event", evt.Name.Name);
        Assert.Equal("schema1", evt.Name.Schema.Name);

        Assert.Equal("ON SCHEDULE AT '2023-01-01 00:00:00'", evt.Schedule.ToSql());
        Assert.Equal("ENABLE", evt.EventEnabledStatus.ToSql());
        Assert.False(evt.OnCompletionPreserve);
        Assert.Equal("UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event')", evt.Body.ToSql());
    }

    [Fact]
    public void Event_Start_Interval_Fails()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE
            DEFINER = `root`@`localhost`
        EVENT `schema1`.`my_event`
            ON SCHEDULE EVERY 1 DAY
            STARTS CURRENT_TIMESTAMP + INTERVAL 1 DAY
            ENDS '2035-01-01 00:00:00'
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        Assert.Throws<DefinitionException.NotLiteralEventDate>(() => DefinitionBuilder.ProcessStatements(statements, Schema, 0));
    }

    [Fact]
    public void Event_End_Interval_Fails()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE
            DEFINER = `root`@`localhost`
        EVENT `schema1`.`my_event`
            ON SCHEDULE EVERY 1 DAY
            STARTS '2023-01-01 00:00:00'
            ENDS CURRENT_TIMESTAMP + INTERVAL 1 MONTH
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        Assert.Throws<DefinitionException.NotLiteralEventDate>(() => DefinitionBuilder.ProcessStatements(statements, Schema, 0));
    }

    [Fact]
    public void Event_At_Interval_Fails()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE
            DEFINER = `root`@`localhost`
        EVENT `schema1`.`my_event`
            ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 MONTH
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        Assert.Throws<DefinitionException.NotLiteralEventDate>(() => DefinitionBuilder.ProcessStatements(statements, Schema, 0));
    }

    [Fact]
    public void Table_Columns_Keys_TextMatch()
    {
        var checkConstraint = AllowCheckOnColumn ? "CHECK (`checked` >= 0)" : "";
        var statementText = $"""
        CREATE TABLE `schema1`.`other_table` (
            `id` INT UNSIGNED NOT NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};

        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
            `val` VARCHAR(100) NULL,
            `last_updated` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            `gen1` INT AS (id + 100) VIRTUAL,
            `gen2` INT GENERATED ALWAYS AS (id + 200) STORED,
            `checked` INT NULL {checkConstraint},
            `other_id` INT UNSIGNED NOT NULL,
            PRIMARY KEY (`id`),
            INDEX `idx_other` (`other_id`),
            CONSTRAINT `uc_table1` UNIQUE (`val`),
            CONSTRAINT `fk_other` FOREIGN KEY (`other_id`) REFERENCES `other_table` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
            FULLTEXT INDEX `ft_idx_val` (`val`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();

        Assert.NotNull(def);
        Assert.Equal(2, def.Tables.Count);
        var tableObj = def.Tables.Values.First(x => x.Name.Name == "table1");
        var table = tableObj.ToCreateStatement(true, GetDifferFormatManager());

        Assert.Equal("table1", table.Name.Values[1].Name);
        Assert.Equal("schema1", table.Name.Values[0].Name);


        var expected1 = $"`id` INT{DefaultUnsignedIntegerWidth} UNSIGNED NOT NULL AUTO_INCREMENT";
        Assert.Equal(expected1, table.Columns![0].ToSql());
        var expected2 = $"`val` VARCHAR(100) CHARACTER SET utf8mb4{DefaultCollationSetter} NULL DEFAULT NULL";
        Assert.Equal(expected2, table.Columns[1].ToSql());

        var currentTimestamp = "CURRENT_TIMESTAMP()";
        var nestOpen = "";
        var nestClose = "";

        var expected3 = $"`last_updated` TIMESTAMP NOT NULL DEFAULT ({currentTimestamp}) ON UPDATE {currentTimestamp}";
        Assert.Equal(expected3, table.Columns[2].ToSql());
        var expected4 = $"`gen1` INT{DefaultIntegerWidth} SIGNED GENERATED ALWAYS AS (`id` + 100) VIRTUAL";
        Assert.Equal(expected4, table.Columns[3].ToSql());
        var expected5 = $"`gen2` INT{DefaultIntegerWidth} SIGNED GENERATED ALWAYS AS (`id` + 200) STORED";
        Assert.Equal(expected5, table.Columns[4].ToSql());
        string check = AllowCheckOnColumn
            ? $" CHECK ({nestOpen}`checked` >= 0{nestClose})"
            : "";

        var expected6 = $"`checked` INT{DefaultIntegerWidth} SIGNED NULL DEFAULT NULL{check}";
        Assert.Equal(expected6, table.Columns[5].ToSql());

        var expectedPk = "PRIMARY KEY USING BTREE (`id` ASC)";
        // Assert.Equal(expectedPk, table.PrimaryKey!.ToSql());
        var actualPks = table.Constraints.Where(x => x is StatementTableConstraint.PrimaryKey).ToList();
        var actualPk = Assert.Single(actualPks);
        Assert.Equal(expectedPk, actualPk.ToSql());

        var actualUks = table.Constraints.Where(x => x is StatementTableConstraint.UniqueConstraint).ToList();
        var actualUk = Assert.Single(actualUks);
        var expectedUq = "CONSTRAINT `uc_table1` UNIQUE KEY USING BTREE (`val` ASC)";
        // Assert.Equal(expectedUq, table.UniqueKeys.Values.First().ToSql());
        Assert.Equal(expectedUq, actualUk.ToSql());

        var actualFks = table.Constraints.Where(x => x is StatementTableConstraint.ForeignKey).ToList();
        var actualFk = Assert.Single(actualFks);

        var expectedFk = "CONSTRAINT `fk_other` FOREIGN KEY (`other_id`) REFERENCES `other_table` (`id`) ON DELETE CASCADE ON UPDATE CASCADE";
        Assert.Equal(expectedFk, actualFk.ToSql());

        var actualInds = table.Constraints.Where(x => x is StatementTableConstraint.Standard).ToList();
        var actualInd = Assert.Single(actualInds);
        var expectedIdx = "KEY `idx_other` USING BTREE (`other_id` ASC)";
        Assert.Equal(expectedIdx, actualInd.ToSql());

        var actualFts = table.Constraints.Where(x => x is StatementTableConstraint.FullText).ToList();
        var actualFt = Assert.Single(actualFts);
        var expectedFtIdx = "FULLTEXT KEY `ft_idx_val` (`val`)";
        // Assert.Equal(expectedFtIdx, table.Keys.Values.First(i => i.Name?.Name == "ft_idx_val").ToSql());
        Assert.Equal(expectedFtIdx, actualFt.ToSql());
    }

    [Fact]
    public void OnUpdate_NeverNested()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            `last_updated` TIMESTAMP NOT NULL ON UPDATE CURRENT_TIMESTAMP,
            `last_updated_paren` TIMESTAMP NOT NULL ON UPDATE (CURRENT_TIMESTAMP),
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();
        var table = def.Tables.Values.First(x => x.Name.Name == "table1");

        var columnNoParen = table.Columns.First(c => c.Name.Name == "last_updated");
        var columnNoParenOnUpdate = columnNoParen.OnUpdate;
        Assert.NotNull(columnNoParenOnUpdate);

        var columnParen = table.Columns.First(c => c.Name.Name == "last_updated_paren");
        var columnParenOnUpdate = columnParen.OnUpdate;
        Assert.NotNull(columnParenOnUpdate);

        var expectedOnUpdate = "CURRENT_TIMESTAMP()";

        Assert.Equal(expectedOnUpdate, columnNoParenOnUpdate.Expression.ToSql());
        Assert.Equal(expectedOnUpdate, columnParenOnUpdate.Expression.ToSql());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("(1)")]
    public void CreateTable_DefaultValue_AsValueRegardlessOfParentheses(string sql)
    {
        var createTable = $"CREATE TABLE mytable (my_id INT NOT NULL, my_val BOOLEAN DEFAULT {sql})";
        var statements = TextParser.ParseText(createTable);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var def = DefinitionBuilder.ToDefinition();
        var table = def.Tables.Values.First(x => x.Name.Name == "mytable");
        var column = table.Columns.First(c => c.Name.Name == "my_val");
        var defaultOption = column.Default;

        Assert.NotNull(defaultOption);
        var defaultValueOption = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(defaultOption);
        Assert.IsType<Value.Number>(defaultValueOption.Value);
    }

    [Fact]
    public void Generated_Nesting_KeptInExpression_RemovedInNormalized_EqualityBasedOnNormalized()
    {
        var statementText1 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            quantity INT NOT NULL,
            price DECIMAL(10,2) NOT NULL,
            threshold INT NOT NULL,
            total DECIMAL(10,2) GENERATED ALWAYS AS (quantity * price) STORED,
            restock_cost DECIMAL(10,2) GENERATED ALWAYS AS (IF(quantity < threshold, price * (threshold - quantity), 0)) STORED
        );
        """;

        var db1 = GetDefinitionBuilder();
        var statements = TextParser.ParseText(statementText1);
        db1.ProcessStatements(statements, Schema, 0);

        var def1 = db1.ToDefinition();
        var table1 = def1.Tables.Values.First(x => x.Name.Name == "mytable");
        var restockCostColumn1 = table1.Columns.First(c => c.Name.Name == "restock_cost");
        var genOption1 = restockCostColumn1.Generated;

        Assert.NotNull(genOption1);

        var genAs1 = genOption1 as ColumnOption.Generated.AsExpression;
        Assert.NotNull(genAs1);

        var statementText2 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            quantity INT NOT NULL,
            price DECIMAL(10,2) NOT NULL,
            threshold INT NOT NULL,
            total DECIMAL(10,2) GENERATED ALWAYS AS (quantity * price) STORED,
            restock_cost DECIMAL(10,2) GENERATED ALWAYS AS (IF(quantity < threshold, price * ((threshold - quantity)), 0)) STORED
        );
        """;
        var statements2 = TextParser.ParseText(statementText2);
        var db2 = GetDefinitionBuilder();
        db2.ProcessStatements(statements2, Schema, 0);

        var def2 = db2.ToDefinition();
        var table2 = def2.Tables.Values.First(x => x.Name.Name == "mytable");
        var restockCostColumn2 = table2.Columns.First(c => c.Name.Name == "restock_cost");
        var genOption2 = restockCostColumn2.Generated;

        Assert.NotNull(genOption2);

        var genAs2 = genOption2 as ColumnOption.Generated.AsExpression;
        Assert.NotNull(genAs2);

        Assert.Equal(genOption1, genOption2);
        Assert.Equal(genAs1, genAs2);

        Assert.Equal(genAs1.NormalizedExpression, genAs2.NormalizedExpression);
        Assert.NotEqual(genAs1.Expression, genAs2.Expression);

        Assert.Contains("((`threshold` - `quantity`))", genAs2.Expression.ToSql());
        Assert.DoesNotContain("((`threshold` - `quantity`))", genAs1.Expression.ToSql());

        var create1 = table1.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        var create2 = table2.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        Assert.NotEqual(create1, create2);
        Assert.Contains("((`threshold` - `quantity`))", create2);

        var thenRight1 = ExtractThenRightOperand(genAs1.Expression);
        var thenRight2 = ExtractThenRightOperand(genAs2.Expression);
        var thenRight1Normalized = ExtractThenRightOperand(genAs1.NormalizedExpression);
        var thenRight2Normalized = ExtractThenRightOperand(genAs2.NormalizedExpression);

        Assert.Equal(1, CountNestedDepth(thenRight1));
        Assert.Equal(2, CountNestedDepth(thenRight2));
        Assert.IsType<Core.Expressions.BinaryOperator>(thenRight1Normalized);
        Assert.IsType<Core.Expressions.BinaryOperator>(thenRight2Normalized);
    }

    [Fact]
    public void TableCheck_Nesting_KeptInExpression_RemovedInNormalized_EqualityBasedOnNormalized()
    {
        var statementText1 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            threshold INT NOT NULL,
            CONSTRAINT chk_restock CHECK (IF(id < threshold, id * (threshold - id), 0))
        );
        """;

        var db1 = GetDefinitionBuilder();
        var statements1 = TextParser.ParseText(statementText1);
        db1.ProcessStatements(statements1, Schema, 0);

        var def1 = db1.ToDefinition();
        var table1 = def1.Tables.Values.First(x => x.Name.Name == "mytable");
        var check1 = table1.Checks.Values.First(c => c.Name?.Name == "chk_restock");

        var statementText2 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            threshold INT NOT NULL,
            CONSTRAINT chk_restock CHECK (IF(id < threshold, id * ((threshold - id)), 0))
        );
        """;

        var db2 = GetDefinitionBuilder();
        var statements2 = TextParser.ParseText(statementText2);
        db2.ProcessStatements(statements2, Schema, 0);

        var def2 = db2.ToDefinition();
        var table2 = def2.Tables.Values.First(x => x.Name.Name == "mytable");
        var check2 = table2.Checks.Values.First(c => c.Name?.Name == "chk_restock");

        Assert.Equal(check1, check2);

        Assert.Equal(check1.NormalizedExpression, check2.NormalizedExpression);
        Assert.NotEqual(check1.Expression, check2.Expression);

        Assert.Contains("((`threshold` - `id`))", check2.Expression.ToSql());
        Assert.DoesNotContain("((`threshold` - `id`))", check1.Expression.ToSql());

        var create1 = table1.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        var create2 = table2.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        Assert.NotEqual(create1, create2);
        Assert.Contains("((`threshold` - `id`))", create2);

        var thenRight1 = ExtractThenRightOperand(check1.Expression);
        var thenRight2 = ExtractThenRightOperand(check2.Expression);
        var thenRight1Normalized = ExtractThenRightOperand(check1.NormalizedExpression);
        var thenRight2Normalized = ExtractThenRightOperand(check2.NormalizedExpression);

        Assert.Equal(1, CountNestedDepth(thenRight1));
        Assert.Equal(2, CountNestedDepth(thenRight2));
        Assert.IsType<Core.Expressions.BinaryOperator>(thenRight1Normalized);
        Assert.IsType<Core.Expressions.BinaryOperator>(thenRight2Normalized);
    }

    [Fact]
    public void DefaultExpression_Nesting_KeptInExpression_RemovedInNormalized_EqualityBasedOnNormalized()
    {
        var statementText1 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            def_col INT DEFAULT (IF(id > 0, id * (5 - 1), 0))
        );
        """;

        var db1 = GetDefinitionBuilder();
        var statements1 = TextParser.ParseText(statementText1);
        db1.ProcessStatements(statements1, Schema, 0);

        var def1 = db1.ToDefinition();
        var table1 = def1.Tables.Values.First(x => x.Name.Name == "mytable");
        var defaultColumn1 = table1.Columns.First(c => c.Name.Name == "def_col");
        var default1 = defaultColumn1.Default as ColumnOption.ColumnDefault.DefaultExpression;

        Assert.NotNull(default1);

        var statementText2 = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            def_col INT DEFAULT (IF(id > 0, id * ((5 - 1)), 0))
        );
        """;

        var db2 = GetDefinitionBuilder();
        var statements2 = TextParser.ParseText(statementText2);
        db2.ProcessStatements(statements2, Schema, 0);

        var def2 = db2.ToDefinition();
        var table2 = def2.Tables.Values.First(x => x.Name.Name == "mytable");
        var defaultColumn2 = table2.Columns.First(c => c.Name.Name == "def_col");
        var default2 = defaultColumn2.Default as ColumnOption.ColumnDefault.DefaultExpression;

        Assert.NotNull(default2);

        Assert.Equal(default1, default2);

        Assert.Equal(default1.NormalizedExpression, default2.NormalizedExpression);
        Assert.NotEqual(default1.Expression, default2.Expression);

        Assert.Contains("((5 - 1))", default2.Expression.ToSql());
        Assert.DoesNotContain("((5 - 1))", default1.Expression.ToSql());

        var create1 = table1.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        var create2 = table2.ToCreateStatement(true, GetDifferFormatManager()).ToSql();
        Assert.NotEqual(create1, create2);
        Assert.Contains("((5 - 1))", create2);

        var thenRight1 = ExtractThenRightOperand(default1.Expression);
        var thenRight2 = ExtractThenRightOperand(default2.Expression);
        var thenRight1Normalized = ExtractThenRightOperand(default1.NormalizedExpression);
        var thenRight2Normalized = ExtractThenRightOperand(default2.NormalizedExpression);

        Assert.Equal(1, CountNestedDepth(thenRight1));
        Assert.Equal(2, CountNestedDepth(thenRight2));
        Assert.IsType<Core.Expressions.BinaryOperator>(thenRight1Normalized);
        Assert.IsType<Core.Expressions.BinaryOperator>(thenRight2Normalized);
    }

    protected static Expression? ExtractThenClause(Expression expr)
    {
        if (expr is FunctionCall func
            && func.Name.Values.Last().Name.Equals("IF", StringComparison.OrdinalIgnoreCase)
            && func.Arguments is FunctionArguments.List args
            && args.Arguments.SafeAny()
            && args.Arguments.Count == 3
            && args.Arguments[1] is FunctionArgument.Unnamed { Argument: FunctionArgumentExpression.FunctionExpression funcExpr })
        {
            return funcExpr.Expression;
        }

        return null;
    }

    protected static Expression ExtractThenRightOperand(Expression expr)
    {
        Expression? thenClause = ExtractThenClause(expr);
        if (thenClause is not Core.Expressions.BinaryOperator thenBinOp)
        {
            throw new XunitException("Expected IF THEN clause to be a binary operator expression.");
        }

        return thenBinOp.Right;
    }

    protected static int CountNestedDepth(Expression expr)
    {
        var depth = 0;
        var current = expr;
        while (current is Nested nested)
        {
            depth++;
            current = nested.Expression;
        }

        return depth;
    }

    [Fact]
    public void Validate_IndexColumnMissing_UniqueKey()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            CONSTRAINT `uc_table1` UNIQUE (`missing_column`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.IndexColumnNotFound>(ex);
    }

    [Fact]
    public void Validate_IndexColumnMissing_RegularKey()
    {
        var statementText = $"""
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            INDEX `idx_table1` (`missing_column`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4{DefaultCollationSetter};
        """;
        var statements = TextParser.ParseText(statementText);

        DefinitionBuilder.ProcessStatements(statements, Schema, 0);

        var errors = Assert.Throws<ValidationExceptionSet>(() =>
        {
            DefinitionBuilder.ToDefinition();
        });

        var ex = Assert.Single(errors.Exceptions);
        Assert.IsType<SqlSyntaxException.IndexColumnNotFound>(ex);
    }

    [Fact]
    public void Validate_Table_NoColumns_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
        """;
        var statements = TextParser.ParseText(statementText);

        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            var table = def.Tables.Values.First(x => x.Name.Name == "table1");
            Assert.Empty(table.Columns);
        }
        else
        {
            Assert.Throws<DefinitionException.MissingTableColumns>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
        }
    }

    [Fact]
    public void Validate_Table_SelectAs_Throws()
    {
        var statementText = """
        CREATE TABLE schema1.table1
        (
            id INT UNSIGNED NOT NULL,
            val VARCHAR(100) NULL
        );

        CREATE TABLE schema1.table2
        AS
        SELECT *
        FROM schema1.table1;
        """;
        var statements = TextParser.ParseText(statementText);


        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Equal(2, def.Tables.Count);
            Assert.Equal("table1", def.Tables.Values.First().Name.Name);
            Assert.Equal("table2", def.Tables.Values.Last().Name.Name);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.CreateTableAsSelect>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.SelectAsTableTemplate, "`mycatalog`.`schema1`.`table2`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
            // Assert.Equal("Not supported in schema definition: CREATE TABLE ... AS SELECT in table `mycatalog`.`schema1`.`table2`.", error.Message);
        }
    }

    [Fact]
    public void Validate_Procedure_Definer_Missing_Throws()
    {
        var statementText = """
        CREATE PROCEDURE `schema1`.`my_procedure` (IN param1 INT, OUT param2 VARCHAR(100))
        BEGIN
            SET param2 = CONCAT(param1, ' is the input');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Procedures);
            var procedure = def.Procedures.FirstOrDefault(p => p.Key.Name == "my_procedure").Value;
            Assert.NotNull(procedure.Definer);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoDefinerTemplate, "Procedure", "`mycatalog`.`schema1`.`my_procedure`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Procedure_Definer_Implicit_Throws()
    {
        var statementText = """
        CREATE DEFINER = CURRENT_USER PROCEDURE `schema1`.`my_procedure` (IN param1 INT, OUT param2 VARCHAR(100))
        BEGIN
            SET param2 = CONCAT(param1, ' is the input');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Procedures);
            var procedure = def.Procedures.FirstOrDefault(p => p.Key.Name == "my_procedure").Value;
            Assert.NotNull(procedure.Definer);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.ImplicitDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ImplicitDefinerTemplate, "Procedure", "`mycatalog`.`schema1`.`my_procedure`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Procedure_SqlSecurity_Missing_Throws()
    {
        var statementText = """
        CREATE DEFINER = `root`@`localhost`
        PROCEDURE `schema1`.`my_procedure` (IN param1 INT, OUT param2 VARCHAR(100))
        BEGIN
            SET param2 = CONCAT(param1, ' is the input');
        END;
        """;
        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Procedures);
            var procedure = def.Procedures.FirstOrDefault(p => p.Key.Name == "my_procedure").Value;
            Assert.NotNull(procedure);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingSecurityContext>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });

            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoSqlSecurityTemplate, "Procedure", "`mycatalog`.`schema1`.`my_procedure`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Function_Definer_Missing_Throws()
    {
        var statementText = """
        CREATE FUNCTION `schema1`.`my_function` (param1 INT) RETURNS VARCHAR(100)
        BEGIN
            RETURN CONCAT(param1, ' is the input');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Functions);
            var function = def.Functions.FirstOrDefault(f => f.Key.Name == "my_function").Value;
            Assert.NotNull(function);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });

            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoDefinerTemplate, "Function", "`mycatalog`.`schema1`.`my_function`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Function_Definer_Implicit_Throws()
    {
        var statementText = """
        CREATE DEFINER = CURRENT_USER FUNCTION `schema1`.`my_function` (param1 INT) RETURNS VARCHAR(100)
        BEGIN
            RETURN CONCAT(param1, ' is the input');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Functions);
            var function = def.Functions.FirstOrDefault(f => f.Key.Name == "my_function").Value;
            Assert.NotNull(function);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.ImplicitDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ImplicitDefinerTemplate, "Function", "`mycatalog`.`schema1`.`my_function`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Function_SqlSecurity_Missing_Throws()
    {
        var statementText = """
        CREATE DEFINER = `root`@`localhost`
        FUNCTION `schema1`.`my_function` (param1 INT) RETURNS VARCHAR(100)
        BEGIN
            RETURN CONCAT(param1, ' is the input');
        END;
        """;
        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Functions);
            var function = def.Functions.FirstOrDefault(f => f.Key.Name == "my_function").Value;
            Assert.NotNull(function);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingSecurityContext>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoSqlSecurityTemplate, "Function", "`mycatalog`.`schema1`.`my_function`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_View_Definer_Missing_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE VIEW `schema1`.`my_view` AS
        SELECT id, val FROM `schema1`.`table1`;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Views);
            var view = def.Views.FirstOrDefault(v => v.Key.Name == "my_view").Value;
            Assert.NotNull(view);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoDefinerTemplate, "View", "`mycatalog`.`schema1`.`my_view`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_View_Definer_Implicit_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = CURRENT_USER VIEW `schema1`.`my_view` AS
        SELECT id, val FROM `schema1`.`table1`;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Views);
            var view = def.Views.FirstOrDefault(v => v.Key.Name == "my_view").Value;
            Assert.NotNull(view);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.ImplicitDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ImplicitDefinerTemplate, "View", "`mycatalog`.`schema1`.`my_view`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_View_SqlSecurity_Missing_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = `root`@`localhost` VIEW `schema1`.`my_view` AS
        SELECT id, val FROM `schema1`.`table1`;
        """;
        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Views);
            var view = def.Views.FirstOrDefault(v => v.Key.Name == "my_view").Value;
            Assert.NotNull(view);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingSecurityContext>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoSqlSecurityTemplate, "View", "`mycatalog`.`schema1`.`my_view`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Event_Missing_Definer_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE EVENT `schema1`.`my_event`
            ON SCHEDULE EVERY 1 DAY
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Events);
            var evt = def.Events.FirstOrDefault(e => e.Key.Name == "my_event").Value;
            Assert.NotNull(evt);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoDefinerTemplate, "Event", "`mycatalog`.`schema1`.`my_event`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Event_Implicit_Definer_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = CURRENT_USER EVENT `schema1`.`my_event`
            ON SCHEDULE EVERY 1 DAY
            ENABLE
        DO
            UPDATE `schema1`.`table1` SET `val` = CONCAT(`val`, ' - updated by event');
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Events);
            var evt = def.Events.FirstOrDefault(e => e.Key.Name == "my_event").Value;
            Assert.NotNull(evt);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.ImplicitDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ImplicitDefinerTemplate, "Event", "`mycatalog`.`schema1`.`my_event`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Trigger_Definer_Missing_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE TRIGGER `schema1`.`my_trigger1`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 1');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Triggers);
            var trigger = def.Triggers.FirstOrDefault(t => t.Key.Name == "my_trigger1").Value;
            Assert.NotNull(trigger);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.MissingDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.NoDefinerTemplate, "Trigger", "`mycatalog`.`schema1`.`my_trigger1`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Trigger_Definer_Implicit_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = CURRENT_USER TRIGGER `schema1`.`my_trigger1`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 1');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Single(def.Triggers);
            var trigger = def.Triggers.FirstOrDefault(t => t.Key.Name == "my_trigger1").Value;
            Assert.NotNull(trigger);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.ImplicitDefiner>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ImplicitDefinerTemplate, "Trigger", "`mycatalog`.`schema1`.`my_trigger1`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Trigger_Order_Precedes_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger1`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 1');
        END;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger2`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
            PRECEDES `schema1`.`my_trigger1`
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 2');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Equal(2, def.Triggers.Count);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.TriggerOrderViaPrecedes>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.TriggerOrderViaPrecedesTemplate, "`mycatalog`.`schema1`.`my_trigger2`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Trigger_Order_Duplicate_Unspecified_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger1`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 1');
        END;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger2`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 2');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Equal(2, def.Triggers.Count);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.UndefinedTriggerOrder>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.TriggerOrderUndefinedTemplate, "`mycatalog`.`schema1`.`my_trigger2`", "`mycatalog`.`schema1`.`my_trigger1`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Validate_Trigger_Order_Duplicate_Specified_Throws()
    {
        var statementText = """
        CREATE TABLE `schema1`.`table1` (
            `id` INT UNSIGNED NOT NULL,
            `val` VARCHAR(100) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger0`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 0');
        END;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger1`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
            FOLLOWS `schema1`.`my_trigger0`
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 1');
        END;

        CREATE DEFINER = `root`@`localhost`
        TRIGGER `schema1`.`my_trigger2`
            BEFORE INSERT ON `schema1`.`table1`
            FOR EACH ROW
            FOLLOWS `schema1`.`my_trigger0`
        BEGIN
            SET NEW.val = CONCAT(NEW.val, ' - modified by trigger 2');
        END;
        """;

        var statements = TextParser.ParseText(statementText);
        if (Relaxed)
        {
            DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            var def = DefinitionBuilder.ToDefinition();
            Assert.Equal(3, def.Triggers.Count);
        }
        else
        {
            var error = Assert.Throws<DefinitionException.UndefinedTriggerOrder>(() =>
            {
                DefinitionBuilder.ProcessStatements(statements, Schema, 0);
            });
            var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.TriggerOrderUndefinedTemplate, "`mycatalog`.`schema1`.`my_trigger2`", "`mycatalog`.`schema1`.`my_trigger1`");
            var actual = error.Message;
            Assert.Equal(expected, actual);
        }
    }

    public class DataTypeSerializer : IXunitSerializer
    {
        public object Deserialize(Type type, string serializedValue)
        {
            var lexer = new MyLexer();
            var parser = new MyParser();
            return parser.DataTypeParser.ParseDataType(new ParserState([.. lexer.Tokenize(serializedValue)]));
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (value is DataType)
            {
                failureReason = "";
                return true;
            }
            failureReason = $"Type {type.FullName} is not a DataType.";
            return false;
        }

        public string Serialize(object value)
        {
            if (value is DataType dataType)
            {
                return dataType.ToSql();
            }
            throw new ArgumentException($"Value {value} is not a DataType.", nameof(value));
        }
    }
}
