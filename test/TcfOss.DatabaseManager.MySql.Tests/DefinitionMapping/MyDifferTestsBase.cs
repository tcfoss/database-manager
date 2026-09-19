using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DefinitionMapping;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using AttributeDefaults = TcfOss.DatabaseManager.Core.Configuration.Parsing.AttributeDefaults;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;
using DefinitionMappingDeployScript = TcfOss.DatabaseManager.Core.DefinitionMapping.DeployScript;
using Refactor = TcfOss.DatabaseManager.Core.DefinitionMapping.Refactor;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionMapping;

public abstract class MyDifferTestsBase
{
    private static readonly CatalogIdentifier s_catalog = new("mycatalog", QuoteStyle.Backticks);
    private static readonly SchemaIdentifier s_schema = new("schema1", s_catalog, QuoteStyle.Backticks);
    private TextParser TextParser { get; } = new(new MyLexer(), new MyParser());

    protected abstract SqlDialect Dialect { get; }
    private bool AllowCheckOnColumn => Dialect == SqlDialect.MariaDb;

    private List<DefinitionAlterStatement> ComputeChanges(string startText, string endText, IEnumerable<Refactor>? refactors = null, Config? rawConfig = null, IEnumerable<DefinitionMappingDeployScript>? deployScripts = null)
    {
        return ComputeChangesWithConfig(startText, endText, refactors, rawConfig, deployScripts).Changes;
    }

    private (MyConfig Config, List<DefinitionAlterStatement> Changes) ComputeChangesWithConfig(string startText, string endText, IEnumerable<Refactor>? refactors = null, Config? rawConfig = null, IEnumerable<DefinitionMappingDeployScript>? deployScripts = null)
    {
        var loggerFactory = new LoggerFactory();
        rawConfig ??= GetRawConfiguration();
        var config = LoadConfig(rawConfig, loggerFactory.CreateLogger<ConfigLoader>());

        var builderLogger = loggerFactory.CreateLogger<MyDefinitionBuilder>();

        var startStatements = TextParser.ParseText(startText);
        var startBuilder = new MyDefinitionBuilder(config, new SourceManager(), new MyFunctionNameProvider(), builderLogger);
        startBuilder.ProcessStatements(startStatements, s_schema, 0);

        var endStatements = TextParser.ParseText(endText);
        var endBuilder = new MyDefinitionBuilder(config, new SourceManager(), new MyFunctionNameProvider(), builderLogger);
        endBuilder.ProcessStatements(endStatements, s_schema, 0);

        var startDefinition = startBuilder.ToDefinition();
        var endDefinition = endBuilder.ToDefinition();

        var differ = new MyDiffer(config, startDefinition, endDefinition, refactors ?? [], deployScripts ?? [], loggerFactory.CreateLogger<MyDiffer>());
        return (config, differ.ComputeChanges());
    }

    private Config GetRawConfiguration(
        int? floatPrecision = null,
        int? floatScale = null,
        int? doublePrecision = null,
        int? doubleScale = null,
        int? decimalPrecision = null,
        int? decimalScale = null,
        bool omitModifiersIfDefault = true
    )
    {
        var attributeDefaults = new AttributeDefaults();
        if (floatPrecision != null)
        {
            attributeDefaults.FloatPrecision = floatPrecision;
        }
        if (floatScale != null)
        {
            attributeDefaults.FloatScale = floatScale;
        }
        if (doublePrecision != null)
        {
            attributeDefaults.DoublePrecision = doublePrecision;
        }
        if (doubleScale != null)
        {
            attributeDefaults.DoubleScale = doubleScale;
        }
        if (decimalPrecision != null)
        {
            attributeDefaults.DecimalPrecision = decimalPrecision;
        }
        if (decimalScale != null)
        {
            attributeDefaults.DecimalScale = decimalScale;
        }

        var differFormattingSettings = new ConfigParsing.DifferFormattingSettings
        {
            OmitModifiersIfDefault = omitModifiersIfDefault,
        };

        var rawConfig = Configuration.TestDataRawConfig.GetMyTestRawConfig(
            dialect: Dialect,
            projectDirectory: "/fake/project/root",
            attributeDefaults: attributeDefaults,
            differFormattingSettings: differFormattingSettings
        );

        foreach (var schema in rawConfig.Schemas)
        {
            schema.SchemaDefaults = new SchemaDefaults
            {
                CharacterSet = "utf8mb4",
                Collation = "utf8mb4_0900_ai_ci",
                Engine = "InnoDB",
            };
        }

        return rawConfig;
    }

    protected static Dictionary<string, object> GetOtherData(Config rawConfig)
    {
        var characterSets = new Dictionary<string, CharacterSetSpec>
        {
            { "utf8mb4", new CharacterSetSpec { CharacterSet = "utf8mb4", DefaultCollation = "utf8mb4_uca1400_ai_ci", Collations = ["utf8mb4_0900_ai_ci", "utf8mb4_uca1400_ai_ci", "utf8mb4_uca1400_ai_cs", "utf8mb4_0900_bin"] } }
        };

        var credentials = new DatabaseCredentials()
        {
            Host = rawConfig.Credentials!.Hostname,
            Port = rawConfig.Credentials.Port != null ? uint.Parse(rawConfig.Credentials.Port) : 3306,
            Username = rawConfig.Credentials.Username!,
            Password = rawConfig.Credentials.Password!,
            ConnectionTimeout = (uint)(rawConfig.Credentials.ConnectionTimeout ?? 30),
        };

        return new Dictionary<string, object>
        {
            { "DatabaseCredentials", credentials },
            { "DatabaseAvailable", true },
            { "ServerDefaults", rawConfig.Schemas.First().SchemaDefaults! },
            { "CharacterSets", characterSets },
            { "SchemaDefaults", rawConfig.Schemas.ToDictionary(s => s.SchemaName, s => s.SchemaDefaults!) },
        };
    }

    protected abstract MyConfig LoadConfig(Config rawConfig, ILogger logger);

    [Fact]
    public void No_Changes()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void PreSetNotNull_DeployScript_With_UniqueId_Generates_Metadata_Insert()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL
        );
        """;

        var scriptId = "00000000-0000-0000-0000-000000000099";
        DefinitionMappingDeployScript[] deployScripts =
        [
            new(
                DeployScriptType.PreSetNotNull,
                s_schema,
                "pre_set_not_null.sql",
                "/fake/project/root/pre_set_not_null.sql",
                [new Truncate(new ObjectName(new Identifier("mytable", QuoteStyle.Backticks)))])
            {
                UniqueId = scriptId,
            }
        ];

        var changes = ComputeChanges(startText, startText, deployScripts: deployScripts);

        Assert.Collection(changes,
            scriptChange =>
            {
                Assert.Equal(DefaultWeights.PreSetNotNullScript, scriptChange.Weight);
                Assert.True(scriptChange.FromDeployScript);
                Assert.Equal("TRUNCATE `mytable`;\n", scriptChange.Statement.ToSql());
            },
            metadataChange =>
            {
                Assert.Equal(DefaultWeights.InsertPreSetNotNullMeta, metadataChange.Weight);
                Assert.False(metadataChange.FromDeployScript);
                Assert.Equal($"INSERT INTO `schema1`.`_database_manager` (`entry_key`, `entry_type`) VALUES ('{scriptId}', 'D')", metadataChange.Statement.ToSql());
            }
        );
    }

    [Fact]
    public void Inferred_End_String_Attributes_Do_Not_Modify_Explicit_Start_Attributes()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void Explicit_End_String_Attributes_Generate_Modify()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL", change.Statement.ToSql());
    }

    [Fact]
    public void Table_Default_String_Attributes_Generate_Table_Change_Without_Column_Modify()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) NOT NULL
        ) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs;
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        Assert.Equal("ALTER TABLE `mytable` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs", change.Statement.ToSql());
    }

    [Fact]
    public void Explicit_Table_Default_Equal_To_Default_Generates_No_Change()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        ) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void Table_Character_Set_Without_Collation_Equal_To_Default_Generates_No_Change()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        ) DEFAULT CHARACTER SET utf8mb4;
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void Table_Collation_Without_Character_Set_Generates_Table_Change()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        ) DEFAULT COLLATE utf8mb4_uca1400_ai_cs;
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        Assert.Equal("ALTER TABLE `mytable` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs", change.Statement.ToSql());
    }

    [Fact]
    public void Table_And_Column_String_Attributes_Generate_Both_Changes()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL
        ) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs;
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(2, changes.Count);
        Assert.Equal("ALTER TABLE `mytable` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs", changes[0].Statement.ToSql());
        Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(255) NOT NULL", changes[1].Statement.ToSql());
    }

    [Fact]
    public void Inferred_End_Attributes_Do_Not_Modify_All_String_Type_Families()
    {
        var startText = """
        CREATE TABLE mytable (
            code CHAR(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
            description TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
            label NATIONAL VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            code CHAR(10) NOT NULL,
            description TEXT NOT NULL,
            label NATIONAL VARCHAR(20) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void Partial_End_Column_Attributes_Ignore_Inferred_Differences()
    {
        var startText = """
        CREATE TABLE mytable (
            charset_only VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL,
            collation_only VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            charset_only VARCHAR(255) CHARACTER SET utf8mb4 NOT NULL,
            collation_only VARCHAR(255) COLLATE utf8mb4_uca1400_ai_cs NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `collation_only` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL", change.Statement.ToSql());
    }

    [Fact]
    public void Mixed_Inferred_Explicit_And_Unchanged_Columns_Modify_Only_Explicit_Change()
    {
        var startText = """
        CREATE TABLE mytable (
            inherited VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
            changed VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
            unchanged VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            inherited VARCHAR(255) NOT NULL,
            changed VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL,
            unchanged VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `changed` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs NOT NULL", change.Statement.ToSql());
    }

    [Fact]
    public void Table_Default_Change_Preserves_Explicit_Old_Column_Attributes()
    {
        var startText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
        ) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs;
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        Assert.Equal("ALTER TABLE `mytable` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_cs", change.Statement.ToSql());
    }

    [Fact]
    public void Change_Data_Type()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id BIGINT NOT NULL,
            name VARCHAR(500) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `id` BIGINT NOT NULL, MODIFY COLUMN `name` VARCHAR(500) NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Make_Nullable()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(255) NULL DEFAULT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Make_Not_Nullable()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(255) NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Make_Not_Nullable_Happens_After_PreSetNotNull_Script()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        DefinitionMappingDeployScript[] deployScripts =
        [
            new(
                DeployScriptType.PreSetNotNull,
                s_schema,
                "pre_set_not_null.sql",
                "/fake/project/root/pre_set_not_null.sql",
                TextParser.ParseText("UPDATE mytable SET name = '' WHERE name IS NULL;"))
        ];

        var changes = ComputeChanges(startText, endText, deployScripts: deployScripts);

        Assert.Collection(changes,
            scriptChange =>
            {
                Assert.Equal(DefaultWeights.PreSetNotNullScript, scriptChange.Weight);
                Assert.True(scriptChange.FromDeployScript);
                Assert.Equal("UPDATE mytable SET name = '' WHERE name IS NULL;\n", scriptChange.Statement.ToSql());
            },
            notNullChange =>
            {
                Assert.Equal(DefaultWeights.SetNotNull, notNullChange.Weight);
                Assert.False(notNullChange.FromDeployScript);
                Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(255) NOT NULL", notNullChange.Statement.ToSql());
            }
        );
    }

    [Fact]
    public void Make_Not_Nullable_With_Other_Change_Happens_After_PreSetNotNull_Script()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(500) NOT NULL
        );
        """;

        DefinitionMappingDeployScript[] deployScripts =
        [
            new(
                DeployScriptType.PreSetNotNull,
                s_schema,
                "pre_set_not_null.sql",
                "/fake/project/root/pre_set_not_null.sql",
                TextParser.ParseText("UPDATE mytable SET name = '' WHERE name IS NULL;"))
        ];

        var changes = ComputeChanges(startText, endText, deployScripts: deployScripts);

        Assert.Collection(changes,
            nullableChange =>
            {
                Assert.Equal(DefaultWeights.AlterTable, nullableChange.Weight);
                Assert.False(nullableChange.FromDeployScript);
                Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(500) NULL", nullableChange.Statement.ToSql());
            },
            scriptChange =>
            {
                Assert.Equal(DefaultWeights.PreSetNotNullScript, scriptChange.Weight);
                Assert.True(scriptChange.FromDeployScript);
                Assert.Equal("UPDATE mytable SET name = '' WHERE name IS NULL;\n", scriptChange.Statement.ToSql());
            },
            notNullChange =>
            {
                Assert.Equal(DefaultWeights.SetNotNull, notNullChange.Weight);
                Assert.False(notNullChange.FromDeployScript);
                Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `name` VARCHAR(500) NOT NULL", notNullChange.Statement.ToSql());
            }
        );
    }

    [Fact]
    public void Add_Default()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL DEFAULT 1,
            name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ALTER COLUMN `id` SET DEFAULT 1";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Remove_Default()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL DEFAULT 1,
            name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ALTER COLUMN `id` DROP DEFAULT";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Change_Float_Type_Attributes()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            some_amount DOUBLE NOT NULL,
            some_smaller_amount FLOAT NOT NULL,
            some_exact_amount DECIMAL NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            some_amount DOUBLE(12,4) UNSIGNED NOT NULL,
            some_smaller_amount FLOAT(9,2) ZEROFILL NOT NULL,
            some_exact_amount DECIMAL(10,2) UNSIGNED NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);

        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `some_amount` DOUBLE(12,4) UNSIGNED NOT NULL, MODIFY COLUMN `some_smaller_amount` FLOAT(9,2) ZEROFILL NOT NULL, MODIFY COLUMN `some_exact_amount` DECIMAL(10,2) UNSIGNED NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Set_Float_Type_Attributes_To_Default()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            some_amount DOUBLE(12,4) UNSIGNED NOT NULL,
            some_smaller_amount FLOAT(9,2) ZEROFILL NOT NULL,
            some_exact_amount DECIMAL(10,2) UNSIGNED NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            some_amount DOUBLE SIGNED NOT NULL,
            some_smaller_amount FLOAT SIGNED NOT NULL,
            some_exact_amount DECIMAL SIGNED NOT NULL
        );
        """;

        var change = Assert.Single(ComputeChanges(startText, endText));

        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `some_amount` DOUBLE NOT NULL, MODIFY COLUMN `some_smaller_amount` FLOAT NOT NULL, MODIFY COLUMN `some_exact_amount` DECIMAL NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Numeric_Defaults_Without_Omit_Modifiers()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            tiny_number TINYINT UNSIGNED ZEROFILL NOT NULL,
            small_number SMALLINT ZEROFILL NOT NULL,
            regular_number INT SIGNED NOT NULL,
            medium_number MEDIUMINT ZEROFILL NOT NULL,
            big_number BIGINT UNSIGNED NOT NULL,
            some_double DOUBLE(12,4) UNSIGNED NOT NULL,
            some_float FLOAT(9,2) ZEROFILL NOT NULL,
            some_decimal DECIMAL(10,2) UNSIGNED NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            tiny_number TINYINT NOT NULL,
            small_number SMALLINT NOT NULL,
            regular_number INT NOT NULL,
            medium_number MEDIUMINT NOT NULL,
            big_number BIGINT NOT NULL,
            some_double DOUBLE(12,4) NOT NULL,
            some_float FLOAT(9,2) NOT NULL,
            some_decimal DECIMAL(10,2) NOT NULL
        );
        """;

        var rawConfig = GetRawConfiguration(omitModifiersIfDefault: false);

        var (config, changes) = ComputeChangesWithConfig(startText, endText, rawConfig: rawConfig);

        var defaultTinyInt = config.AttributeDefaults.SignedTinyIntWidth != null ? $"({config.AttributeDefaults.SignedTinyIntWidth})" : null;
        var defaultSmallInt = config.AttributeDefaults.SignedSmallIntWidth != null ? $"({config.AttributeDefaults.SignedSmallIntWidth})" : null;
        var defaultMediumIntWidth = config.AttributeDefaults.SignedMediumIntWidth != null ? $"({config.AttributeDefaults.SignedMediumIntWidth})" : null;
        var defaultBigIntWidth = config.AttributeDefaults.SignedBigIntWidth != null ? $"({config.AttributeDefaults.SignedBigIntWidth})" : null;

        var change = Assert.Single(changes);

        var expected = $"ALTER TABLE `mytable` MODIFY COLUMN `tiny_number` TINYINT{defaultTinyInt} SIGNED NOT NULL, MODIFY COLUMN `small_number` SMALLINT{defaultSmallInt} SIGNED NOT NULL, MODIFY COLUMN `medium_number` MEDIUMINT{defaultMediumIntWidth} SIGNED NOT NULL, MODIFY COLUMN `big_number` BIGINT{defaultBigIntWidth} SIGNED NOT NULL, MODIFY COLUMN `some_double` DOUBLE(12,4) SIGNED NOT NULL, MODIFY COLUMN `some_float` FLOAT(9,2) SIGNED NOT NULL, MODIFY COLUMN `some_decimal` DECIMAL(10,2) SIGNED NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Numeric_Change_To_Default_Precision()
    {
        var rawConfig = GetRawConfiguration(
            floatPrecision: 5,
            floatScale: 3,
            doublePrecision: 10,
            doubleScale: 5,
            decimalPrecision: 12,
            decimalScale: 3
        );
        // MyConfig.AttributeDefaults.FloatPrecision = new NumericLength.PrecisionScale(5, 3);
        // MyConfig.AttributeDefaults.DoublePrecision = new NumericLength.PrecisionScale(10, 5);
        // MyConfig.AttributeDefaults.DecimalPrecision = new NumericLength.PrecisionScale(12, 3);

        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            some_double DOUBLE(8,3) NOT NULL,
            some_float FLOAT(7,4) NOT NULL,
            some_decimal DECIMAL(12,5) NOT NULL
        )
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            some_double DOUBLE(10,5) NOT NULL,
            some_float FLOAT(5,3) NOT NULL,
            some_decimal DECIMAL(12,3) NOT NULL
        )
        """;

        var changes = ComputeChanges(startText, endText, rawConfig: rawConfig);
        var change = Assert.Single(changes);

        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `some_double` DOUBLE NOT NULL, MODIFY COLUMN `some_float` FLOAT NOT NULL, MODIFY COLUMN `some_decimal` DECIMAL NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Change_Integer_Widths()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            tiny_number TINYINT NOT NULL,
            small_number SMALLINT NOT NULL,
            regular_number INT NOT NULL,
            medium_number MEDIUMINT NOT NULL,
            big_number BIGINT NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT(8) NOT NULL,
            tiny_number TINYINT(3) NOT NULL,
            small_number SMALLINT(5) NOT NULL,
            regular_number INT(10) NOT NULL,
            medium_number MEDIUMINT(7) NOT NULL,
            big_number BIGINT(12) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `id` INT(8) NOT NULL, MODIFY COLUMN `tiny_number` TINYINT(3) NOT NULL, MODIFY COLUMN `small_number` SMALLINT(5) NOT NULL, MODIFY COLUMN `regular_number` INT(10) NOT NULL, MODIFY COLUMN `medium_number` MEDIUMINT(7) NOT NULL, MODIFY COLUMN `big_number` BIGINT(12) NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Remove_Integer_Widths()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT(8) NOT NULL,
            tiny_number TINYINT(3) NOT NULL,
            small_number SMALLINT(5) NOT NULL,
            regular_number INT(10) NOT NULL,
            medium_number MEDIUMINT(7) NOT NULL,
            big_number BIGINT(12) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            tiny_number TINYINT NOT NULL,
            small_number SMALLINT NOT NULL,
            regular_number INT NOT NULL,
            medium_number MEDIUMINT NOT NULL,
            big_number BIGINT NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `id` INT NOT NULL, MODIFY COLUMN `tiny_number` TINYINT NOT NULL, MODIFY COLUMN `small_number` SMALLINT NOT NULL, MODIFY COLUMN `regular_number` INT NOT NULL, MODIFY COLUMN `medium_number` MEDIUMINT NOT NULL, MODIFY COLUMN `big_number` BIGINT NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Generation()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255) GENERATED ALWAYS AS (CONCAT('Name: ', name)) STORED
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            name VARCHAR(255)
        );
        """;

        var changes = ComputeChanges(startText, endText);

        var change = Assert.Single(changes);

        var alterStatement = change.Statement as StatementGroup;
        Assert.NotNull(alterStatement);
        Assert.Equal(4, alterStatement.Substatements.Count);

        var first = alterStatement.Substatements[0] as AlterTable;
        Assert.NotNull(first);
        var firstOp = Assert.Single(first.Operations) as AlterTableOperation.RenameColumn;
        Assert.NotNull(firstOp);
        var tempName = firstOp.NewName.Name;
        var colName = firstOp.OldName.Name;

        var second = alterStatement.Substatements[1] as AlterTable;
        Assert.NotNull(second);
        var secondOp = Assert.Single(second.Operations) as AlterTableOperation.AddColumn;
        Assert.NotNull(secondOp);
        Assert.Equal(colName, secondOp.Column.Name.Name);

        var third = alterStatement.Substatements[2] as Update;
        Assert.NotNull(third);
        var assignment = Assert.Single(third.Assignments);
        var assignmentTarget = assignment.Target as AssignmentTarget.ObjectName;
        Assert.Equal(colName, assignmentTarget!.Name.Values[0].Name);
        var assignmentValue = assignment.Value as SingleIdentifier;
        Assert.Equal(tempName, assignmentValue!.Identifier.Name);

        var fourth = alterStatement.Substatements[3] as AlterTable;
        Assert.NotNull(fourth);
        var fourthOp = Assert.Single(fourth.Operations) as AlterTableOperation.DropColumn;
        Assert.NotNull(fourthOp);
        Assert.Equal(tempName, fourthOp.Column.Name);
    }

    [Fact]
    public void Drop_Generation_Plus_Other_Changes()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(100) NOT NULL,
            last_name VARCHAR(100) NOT NULL,
            full_name VARCHAR(255) GENERATED ALWAYS AS (CONCAT('Name: ', first_name, ' ', last_name)) STORED,
            age INT NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL,
            full_name VARCHAR(255),
            age INT NOT NULL DEFAULT 30
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(3, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Equal(2, first.Operations.Count);
        var firstOp = first.Operations[0] as AlterTableOperation.ModifyColumn;
        Assert.NotNull(firstOp);
        Assert.Equal("MODIFY COLUMN `first_name` VARCHAR(255) NOT NULL", firstOp.ToSql());
        var secondOp = first.Operations[1] as AlterTableOperation.ModifyColumn;
        Assert.NotNull(secondOp);
        Assert.Equal("MODIFY COLUMN `last_name` VARCHAR(255) NOT NULL", secondOp.ToSql());

        var second = changes[1].Statement as StatementGroup;
        Assert.NotNull(second);
        Assert.Equal(4, second.Substatements.Count);

        var subFirst = second.Substatements[0] as AlterTable;
        Assert.NotNull(subFirst);
        var subFirstOp = Assert.Single(subFirst.Operations) as AlterTableOperation.RenameColumn;
        Assert.NotNull(subFirstOp);
        var tempName = subFirstOp.NewName.Name;
        var colName = subFirstOp.OldName.Name;

        var subSecond = second.Substatements[1] as AlterTable;
        Assert.NotNull(subSecond);
        var subSecondOp = Assert.Single(subSecond.Operations) as AlterTableOperation.AddColumn;
        Assert.NotNull(subSecondOp);
        Assert.Equal(colName, subSecondOp.Column.Name.Name);

        var subThird = second.Substatements[2] as Update;
        Assert.NotNull(subThird);
        var assignment = Assert.Single(subThird.Assignments);
        var assignmentTarget = assignment.Target as AssignmentTarget.ObjectName;
        Assert.Equal(colName, assignmentTarget!.Name.Values[0].Name);
        var assignmentValue = assignment.Value as SingleIdentifier;
        Assert.Equal(tempName, assignmentValue!.Identifier.Name);

        var subFourth = second.Substatements[3] as AlterTable;
        Assert.NotNull(subFourth);
        var subFourthOp = Assert.Single(subFourth.Operations) as AlterTableOperation.DropColumn;
        Assert.NotNull(subFourthOp);
        Assert.Equal(tempName, subFourthOp.Column.Name);

        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        var thirdOp = Assert.Single(third.Operations) as AlterTableOperation.SetDefault;
        Assert.NotNull(thirdOp);
        Assert.Equal("ALTER COLUMN `age` SET DEFAULT 30", thirdOp.ToSql());
    }

    [Fact]
    public void Add_Generation_Throws()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(100) NOT NULL,
            last_name VARCHAR(100) NOT NULL,
            full_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(100) NOT NULL,
            last_name VARCHAR(100) NOT NULL,
            full_name VARCHAR(255) GENERATED ALWAYS AS (CONCAT('Name: ', first_name, ' ', last_name)) STORED
        );
        """;

        Assert.Throws<InvalidOperationException>(() => ComputeChanges(startText, endText));
    }

    [Fact]
    public void Generation_Same_Except_Case()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL,
            full_name VARCHAR(255) GENERATED ALWAYS AS (CONCAT('Name: ', first_name, ' ', last_name)) STORED
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL,
            full_name VARCHAR(255) GENERATED ALWAYS AS (concat('Name: ', `first_name`, ' ', `last_name`)) STORED
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void Generation_Change_Expression()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL,
            full_name VARCHAR(255) GENERATED ALWAYS AS (CONCAT('Name: ', first_name, ' ', last_name)) STORED
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL,
            full_name VARCHAR(255) GENERATED ALWAYS AS (concat('Name: ', UPPER(first_name), ' ', UPPER(last_name))) STORED
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);

        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `full_name` VARCHAR(255) GENERATED ALWAYS AS (CONCAT('Name: ', UPPER(`first_name`), ' ', UPPER(`last_name`))) STORED";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Generation_Expression_Nesting()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            quantity INT NOT NULL,
            price DECIMAL(10,2) NOT NULL,
            threshold INT NOT NULL,
            total DECIMAL(10,2) GENERATED ALWAYS AS (quantity * price) STORED,
            restock_cost DECIMAL(10,2) GENERATED ALWAYS AS (IF(quantity < threshold, price * (threshold - quantity), 0)) STORED
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            quantity INT NOT NULL,
            price DECIMAL(10,2) NOT NULL,
            threshold INT NOT NULL,
            total DECIMAL(10,2) GENERATED ALWAYS AS (quantity * price) STORED,
            restock_cost DECIMAL(10,2) GENERATED ALWAYS AS (IF(quantity < threshold, price * ((threshold - quantity)), 0)) STORED
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Empty(changes);
    }

    [Fact]
    public void Add_Column_At_End()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD COLUMN `last_name` VARCHAR(255) NOT NULL AFTER `first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Column_At_Start()
    {
        var startText = """
        CREATE TABLE mytable (
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD COLUMN `id` INT NOT NULL FIRST";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Column_Between()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD COLUMN `first_name` VARCHAR(255) NOT NULL AFTER `id`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Move_Column()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            last_name VARCHAR(255) NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `last_name` VARCHAR(255) NOT NULL AFTER `id`, MODIFY COLUMN `first_name` VARCHAR(255) NOT NULL AFTER `last_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Column()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` DROP COLUMN `first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Rename_Column()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            given_name VARCHAR(255) NOT NULL
        );
        """;

        var refactors = new List<Refactor>
        {
            new Refactor.ColumnRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ColumnIdentifier("first_name", new ObjectIdentifier("mytable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ColumnIdentifier("given_name", new ObjectIdentifier("mytable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks)
            )
        };

        var changes = ComputeChanges(startText, endText, refactors);
        Assert.Equal(2, changes.Count);
        var expected = "ALTER TABLE `mytable` RENAME COLUMN `first_name` TO `given_name`";
        Assert.Equal(expected, changes[0].Statement.ToSql());
        Assert.IsType<Insert>(changes[1].Statement, exactMatch: false);
    }

    [Fact]
    public void Add_Column_With_Default()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;
        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL DEFAULT 'Doe'
        );
        """;
        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD COLUMN `last_name` VARCHAR(255) NOT NULL DEFAULT 'Doe' AFTER `first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Column_With_Comment()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;
        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL COMMENT 'Last name of the person'
        );
        """;
        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD COLUMN `last_name` VARCHAR(255) NOT NULL COMMENT 'Last name of the person' AFTER `first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Change_Primary_Key_Data_Type()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id BIGINT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `id` BIGINT NOT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Change_Primary_Key_Auto_Increment_Data_Type()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL AUTO_INCREMENT,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id BIGINT NOT NULL AUTO_INCREMENT,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Primary_Key_Removes_Auto_Increment()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL AUTO_INCREMENT,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(2, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        var firstExpected = "ALTER TABLE `mytable` MODIFY COLUMN `id` INT NOT NULL";
        Assert.Equal(firstExpected, first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        var secondExpected = "ALTER TABLE `mytable` DROP PRIMARY KEY";
        Assert.Equal(secondExpected, second.ToSql());
    }

    [Fact]
    public void Drop_Primary_Key_Column_With_Auto_Increment()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL AUTO_INCREMENT,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(3, changes.Count);

        // The first change modifies the column to remove AUTO_INCREMENT
        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        var firstExpected = "ALTER TABLE `mytable` MODIFY COLUMN `id` INT NOT NULL";
        Assert.Equal(firstExpected, first.ToSql());

        // The second change drops the primary key and the column
        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        var secondExpected = "ALTER TABLE `mytable` DROP PRIMARY KEY";
        Assert.Equal(secondExpected, second.ToSql());

        // The third change drops the column
        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        Assert.Single(third.Operations);
        var thirdExpected = "ALTER TABLE `mytable` DROP COLUMN `id`";
        Assert.Equal(thirdExpected, third.ToSql());
    }

    [Fact]
    public void Add_Column_With_Check()
    {
        string? checkConstraint = null;
        if (AllowCheckOnColumn)
        {
            string checkExpression = "(`age` >= 0)";
            checkConstraint = $" CHECK {checkExpression}";
        }

        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = $"""
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            age INT{checkConstraint}
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);

        var expected = $"ALTER TABLE `mytable` ADD COLUMN `age` INT NULL DEFAULT NULL{checkConstraint} AFTER `first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Check_To_Existing_Column()
    {
        if (!AllowCheckOnColumn)
        {
            return;
        }

        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            age INT
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            age INT CHECK (age >= 0)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var checkExpression = "(`age` >= 0)";
        var expected = $"ALTER TABLE `mytable` MODIFY COLUMN `age` INT NULL DEFAULT NULL CHECK {checkExpression}";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Check_Constraint_From_Column()
    {
        if (!AllowCheckOnColumn)
        {
            return;
        }
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            age INT CHECK (age >= 0)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            age INT
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `age` INT NULL DEFAULT NULL";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Column_With_Comment_And_Default()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL DEFAULT 'Doe' COMMENT 'Last name of the person'
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD COLUMN `last_name` VARCHAR(255) NOT NULL DEFAULT 'Doe' COMMENT 'Last name of the person' AFTER `first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Multiple_Changes()
    {
        var checkConstraint = "";
        if (AllowCheckOnColumn)
        {
            checkConstraint = " CHECK (age >= 0)";
        }
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            last_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = $"""
        CREATE TABLE mytable (
            id BIGINT NOT NULL,
            first_name VARCHAR(500) NOT NULL,
            last_name VARCHAR(255) NOT NULL DEFAULT 'Doe' COMMENT 'Last name of the person',
            age INT{checkConstraint}
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);

        var checkExpressionAfter = "";
        if (AllowCheckOnColumn)
        {
            checkExpressionAfter = "(`age` >= 0)";
            checkExpressionAfter = $" CHECK {checkExpressionAfter}";
        }

        var alterTable = change.Statement as AlterTable;
        Assert.NotNull(alterTable);
        Assert.Equal(4, alterTable.Operations.Count);
        Assert.Equal("MODIFY COLUMN `id` BIGINT NOT NULL", alterTable.Operations[0].ToSql());
        Assert.Equal("MODIFY COLUMN `first_name` VARCHAR(500) NOT NULL", alterTable.Operations[1].ToSql());
        Assert.Equal("MODIFY COLUMN `last_name` VARCHAR(255) NOT NULL DEFAULT 'Doe' COMMENT 'Last name of the person'", alterTable.Operations[2].ToSql());
        Assert.Equal($"ADD COLUMN `age` INT NULL DEFAULT NULL{checkExpressionAfter} AFTER `last_name`", alterTable.Operations[3].ToSql());
    }

    [Fact]
    public void Add_Primary_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD PRIMARY KEY (`id`)";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Primary_Key_With_Auto_Increment()
    {
        var startText = """
        CREATE TABLE mytable (
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL AUTO_INCREMENT,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(3, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD COLUMN `id` INT NOT NULL FIRST", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD PRIMARY KEY (`id`)", second.ToSql());

        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        Assert.Single(third.Operations);
        Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `id` INT NOT NULL AUTO_INCREMENT", third.ToSql());
    }

    [Fact]
    public void Add_Auto_Increment_Existing_Primary_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL AUTO_INCREMENT,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` MODIFY COLUMN `id` INT NOT NULL AUTO_INCREMENT";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Primary_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` DROP PRIMARY KEY";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Unique_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            CONSTRAINT `idx_first_name` UNIQUE KEY (`first_name`)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD CONSTRAINT `idx_first_name` UNIQUE KEY (`first_name`)";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Unique_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            CONSTRAINT `idx_first_name` UNIQUE KEY (`first_name`)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` DROP CONSTRAINT `idx_first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Index()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            INDEX idx_first_name (first_name)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` ADD KEY `idx_first_name` (`first_name`)";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Index()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            INDEX idx_first_name (first_name)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` DROP KEY `idx_first_name`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Add_Foreign_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);

        Assert.Equal(2, changes.Count);
        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD KEY `idx_mytable_other_id` (`other_id`)", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (`other_id`) REFERENCES `othertable` (`id`)", second.ToSql());
    }

    [Fact]
    public void Drop_Foreign_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` DROP FOREIGN KEY `fk_mytable_other_id`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Foreign_Key_And_Index()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(2, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` DROP FOREIGN KEY `fk_mytable_other_id`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable` DROP KEY `idx_mytable_other_id`", second.ToSql());
    }

    [Fact]
    public void Change_Data_Type_In_Foreign_Key()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id BIGINT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id BIGINT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(4, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` DROP FOREIGN KEY `fk_mytable_other_id`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable` MODIFY COLUMN `other_id` BIGINT NOT NULL", second.ToSql());

        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        Assert.Single(third.Operations);
        Assert.Equal("ALTER TABLE `othertable` MODIFY COLUMN `id` BIGINT NOT NULL", third.ToSql());

        var fourth = changes[3].Statement as AlterTable;
        Assert.NotNull(fourth);
        Assert.Single(fourth.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (`other_id`) REFERENCES `othertable` (`id`)", fourth.ToSql());
    }

    [Fact]
    public void ForeignKey_BackingIndexChange_DropsAddsForeignKey()
    {
        var startText = """
        CREATE TABLE version (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        CREATE TABLE architecture (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        CREATE TABLE file_download(
            id INT NOT NULL,
            version_id INT NOT NULL,
            architecture_id INT NOT NULL,
            file_path VARCHAR(255) NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_file_download_version_id` (`version_id`),
            KEY `idx_file_download_architecture_id` (`architecture_id`),
            CONSTRAINT `fk_file_download_version_id` FOREIGN KEY (version_id) REFERENCES version(id),
            CONSTRAINT `fk_file_download_architecture_id` FOREIGN KEY (architecture_id) REFERENCES architecture(id)
        );
        """;

        var endText = """
        CREATE TABLE version (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        CREATE TABLE architecture (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        CREATE TABLE file_download(
            id INT NOT NULL,
            version_id INT NOT NULL,
            architecture_id INT NOT NULL,
            file_path VARCHAR(255) NOT NULL,
            PRIMARY KEY (id),
            CONSTRAINT `uidx_file_download_version_architecture` UNIQUE KEY (`version_id`, `architecture_id`),
            KEY `idx_file_download_architecture_id` (`architecture_id`),
            CONSTRAINT `fk_file_download_architecture_id` FOREIGN KEY (architecture_id) REFERENCES architecture(id),
            CONSTRAINT `fk_file_download_version_id` FOREIGN KEY (version_id) REFERENCES version(id)
        );
        """;

        var changes = ComputeChanges(startText, endText);

        Assert.Equal(4, changes.Count);
        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `file_download` DROP FOREIGN KEY `fk_file_download_version_id`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `file_download` DROP KEY `idx_file_download_version_id`", second.ToSql());

        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        Assert.Single(third.Operations);
        Assert.Equal("ALTER TABLE `file_download` ADD CONSTRAINT `uidx_file_download_version_architecture` UNIQUE KEY (`version_id`, `architecture_id`)", third.ToSql());

        var fourth = changes[3].Statement as AlterTable;
        Assert.NotNull(fourth);
        Assert.Single(fourth.Operations);
        Assert.Equal("ALTER TABLE `file_download` ADD CONSTRAINT `fk_file_download_version_id` FOREIGN KEY (`version_id`) REFERENCES `version` (`id`)", fourth.ToSql());
    }

    [Fact]
    public void ForeignKey_ReferencedBackingIndexChange_DropsAddsForeignKey()
    {
        var startText = """
        CREATE TABLE version (
            id INT NOT NULL,
            version VARCHAR(10) NOT NULL,
            build VARCHAR(10) NOT NULL,
            PRIMARY KEY (id),
            CONSTRAINT `uidx_version_name_build` UNIQUE (`version`, `build`)
        );
        CREATE TABLE file_download(
            id INT NOT NULL,
            version VARCHAR(10) NOT NULL,
            build VARCHAR(10) NOT NULL,
            file_path VARCHAR(255) NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_version_build` (`version`, `build`),
            CONSTRAINT `fk_version_build` FOREIGN KEY (version, build) REFERENCES version(version, build)
        );
        """;

        var endText = """
        CREATE TABLE version (
            id INT NOT NULL,
            version VARCHAR(10) NOT NULL,
            build VARCHAR(10) NOT NULL,
            PRIMARY KEY (id),
            INDEX `idx_version_name_build` (`version`, `build`)
        );
        CREATE TABLE file_download(
            id INT NOT NULL,
            version VARCHAR(10) NOT NULL,
            build VARCHAR(10) NOT NULL,
            file_path VARCHAR(255) NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_version_build` (`version`, `build`),
            CONSTRAINT `fk_version_build` FOREIGN KEY (version, build) REFERENCES version(version, build)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(4, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `file_download` DROP FOREIGN KEY `fk_version_build`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `version` DROP CONSTRAINT `uidx_version_name_build`", second.ToSql());

        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        Assert.Single(third.Operations);
        Assert.Equal("ALTER TABLE `version` ADD KEY `idx_version_name_build` (`version`, `build`)", third.ToSql());

        var fourth = changes[3].Statement as AlterTable;
        Assert.NotNull(fourth);
        Assert.Single(fourth.Operations);
        Assert.Equal("ALTER TABLE `file_download` ADD CONSTRAINT `fk_version_build` FOREIGN KEY (`version`, `build`) REFERENCES `version` (`version`, `build`)", fourth.ToSql());
    }

    [Fact]
    public void ChangePrimaryKey_UsedInForeignKey_DropsAddsForeignKey()
    {
        var startText = """
        CREATE TABLE branch (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE user (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE permission (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE user_permission (
            user_id INT NOT NULL,
            permission_id INT NOT NULL,
            PRIMARY KEY (user_id, permission_id),
            KEY `idx_user_permission_permission_id` (`permission_id`),
            CONSTRAINT `fk_user_permission_user_id` FOREIGN KEY (user_id) REFERENCES user(id),
            CONSTRAINT `fk_user_permission_permission_id` FOREIGN KEY (permission_id) REFERENCES permission(id)
        );
        """;

        var endText = """
        CREATE TABLE branch (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE user (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE permission (
            id INT NOT NULL,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );

        CREATE TABLE user_permission (
            user_id INT NOT NULL,
            branch_id INT NOT NULL,
            permission_id INT NOT NULL,
            PRIMARY KEY (user_id, branch_id, permission_id),
            KEY `idx_user_permission_branch_id` (`branch_id`),
            KEY `idx_user_permission_permission_id` (`permission_id`),
            CONSTRAINT `fk_user_permission_user_id` FOREIGN KEY (user_id) REFERENCES user(id),
            CONSTRAINT `fk_user_permission_branch_id` FOREIGN KEY (branch_id) REFERENCES branch(id),
            CONSTRAINT `fk_user_permission_permission_id` FOREIGN KEY (permission_id) REFERENCES permission(id)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(7, changes.Count);

        AlterTable? stmt = changes[0].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` DROP FOREIGN KEY `fk_user_permission_user_id`", stmt.ToSql());

        stmt = changes[1].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` DROP PRIMARY KEY", stmt.ToSql());

        stmt = changes[2].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` ADD COLUMN `branch_id` INT NOT NULL AFTER `user_id`", stmt.ToSql());

        stmt = changes[3].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` ADD KEY `idx_user_permission_branch_id` (`branch_id`)", stmt.ToSql());

        stmt = changes[4].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` ADD PRIMARY KEY (`user_id`, `branch_id`, `permission_id`)", stmt.ToSql());

        stmt = changes[5].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` ADD CONSTRAINT `fk_user_permission_branch_id` FOREIGN KEY (`branch_id`) REFERENCES `branch` (`id`)", stmt.ToSql());

        stmt = changes[6].Statement as AlterTable;
        Assert.NotNull(stmt);
        Assert.Single(stmt.Operations);
        Assert.Equal("ALTER TABLE `user_permission` ADD CONSTRAINT `fk_user_permission_user_id` FOREIGN KEY (`user_id`) REFERENCES `user` (`id`)", stmt.ToSql());
    }

    [Fact]
    public void Multiple_Refactors()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable_newname (
            id INT NOT NULL,
            given_name VARCHAR(255) NOT NULL
        );
        """;

        var refactors = new List<Refactor>
        {
            new Refactor.TableRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ObjectIdentifier("mytable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ObjectIdentifier("mytable_newname", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks)
            ),
            new Refactor.ColumnRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ColumnIdentifier("first_name", new ObjectIdentifier("mytable_newname", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ColumnIdentifier("given_name", new ObjectIdentifier("mytable_newname", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks)
            )
        };

        var changes = ComputeChanges(startText, endText, refactors);
        Assert.Equal(3, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` RENAME TO `mytable_newname`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` RENAME COLUMN `first_name` TO `given_name`", second.ToSql());

        var third = changes[2].Statement as Insert;
        Assert.NotNull(third);
        Assert.Equal("schema1", third.Name.Values[0].Name);
        Assert.Equal(StoredMetadataConstants.TableName, third.Name.Values[1].Name);
    }

    [Fact]
    public void Multiple_Refactors_With_Foreign_Keys()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable_newname (
            id INT NOT NULL,
            other_newname_id BIGINT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_newname_id` (`other_newname_id`),
            CONSTRAINT `fk_mytable_other_newname_id` FOREIGN KEY (other_newname_id) REFERENCES othertable_newname(id)
        );

        CREATE TABLE othertable_newname (
            id BIGINT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var refactors = new List<Refactor>
        {
            new Refactor.TableRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ObjectIdentifier("mytable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ObjectIdentifier("mytable_newname", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks)
            ),
            new Refactor.TableRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ObjectIdentifier("othertable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ObjectIdentifier("othertable_newname", new SchemaIdentifier("schema1",  s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks)
            ),
            new Refactor.ColumnRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ColumnIdentifier("other_id", new ObjectIdentifier("mytable_newname", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ColumnIdentifier("other_newname_id", new ObjectIdentifier("mytable_newname", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks)
            )
        };

        var changes = ComputeChanges(startText, endText, refactors);
        Assert.Equal(10, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` RENAME TO `mytable_newname`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `othertable` RENAME TO `othertable_newname`", second.ToSql());

        var third = changes[2].Statement as AlterTable;
        Assert.NotNull(third);
        Assert.Single(third.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` RENAME COLUMN `other_id` TO `other_newname_id`", third.ToSql());

        var fourth = changes[3].Statement as Insert;
        Assert.NotNull(fourth);
        Assert.Equal("schema1", fourth.Name.Values[0].Name);
        Assert.Equal(StoredMetadataConstants.TableName, fourth.Name.Values[1].Name);

        var fifth = changes[4].Statement as AlterTable;
        Assert.NotNull(fifth);
        Assert.Single(fifth.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` DROP FOREIGN KEY `fk_mytable_other_id`", fifth.ToSql());

        var sixth = changes[5].Statement as AlterTable;
        Assert.NotNull(sixth);
        Assert.Single(sixth.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` DROP KEY `idx_mytable_other_id`", sixth.ToSql());

        var seventh = changes[6].Statement as AlterTable;
        Assert.NotNull(seventh);
        Assert.Single(seventh.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` MODIFY COLUMN `other_newname_id` BIGINT NOT NULL", seventh.ToSql());

        var eighth = changes[7].Statement as AlterTable;
        Assert.NotNull(eighth);
        Assert.Single(eighth.Operations);
        Assert.Equal("ALTER TABLE `othertable_newname` MODIFY COLUMN `id` BIGINT NOT NULL", eighth.ToSql());

        var ninth = changes[8].Statement as AlterTable;
        Assert.NotNull(ninth);
        Assert.Single(ninth.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` ADD KEY `idx_mytable_other_newname_id` (`other_newname_id`)", ninth.ToSql());

        var tenth = changes[9].Statement as AlterTable;
        Assert.NotNull(tenth);
        Assert.Single(tenth.Operations);
        Assert.Equal("ALTER TABLE `mytable_newname` ADD CONSTRAINT `fk_mytable_other_newname_id` FOREIGN KEY (`other_newname_id`) REFERENCES `othertable_newname` (`id`)", tenth.ToSql());
    }

    [Fact]
    public void Rename_Referenced_Foreign_Key_Column()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(id)
        );

        CREATE TABLE othertable (
            id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (id)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            other_id INT NOT NULL,
            PRIMARY KEY (id),
            KEY `idx_mytable_other_id` (`other_id`),
            CONSTRAINT `fk_mytable_other_id` FOREIGN KEY (other_id) REFERENCES othertable(new_id)
        );

        CREATE TABLE othertable (
            new_id INT NOT NULL,
            description VARCHAR(255) NOT NULL,
            PRIMARY KEY (new_id)
        );
        """;

        var refactors = new List<Refactor>
        {
            new Refactor.ColumnRename(
                Guid.NewGuid().ToString(),
                new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks),
                new ColumnIdentifier("id", new ObjectIdentifier("othertable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ColumnIdentifier("new_id", new ObjectIdentifier("othertable", new SchemaIdentifier("schema1", s_catalog, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks)
            )
        };

        var changes = ComputeChanges(startText, endText, refactors);
        Assert.Equal(2, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `othertable` RENAME COLUMN `id` TO `new_id`", first.ToSql());

        var second = changes[1].Statement as Insert;
        Assert.NotNull(second);
        Assert.Equal("schema1", second.Name.Values[0].Name);
        Assert.Equal(StoredMetadataConstants.TableName, second.Name.Values[1].Name);
    }

    [Fact]
    public void Add_Table_Check()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            CONSTRAINT chk_mytable_id CHECK (id > 0)
        );
        """;

        var changes = ComputeChanges(startText, endText);

        var change = Assert.Single(changes);
        var checkCondition = "`id` > 0";
        var expected = $"ALTER TABLE `mytable` ADD CONSTRAINT `chk_mytable_id` CHECK ({checkCondition})";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Drop_Table_Check()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            CONSTRAINT chk_mytable_id CHECK (id > 0)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL
        );
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);
        var expected = "ALTER TABLE `mytable` DROP CONSTRAINT `chk_mytable_id`";
        Assert.Equal(expected, change.Statement.ToSql());
    }

    [Fact]
    public void Change_Table_Check()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            CONSTRAINT chk_mytable_id CHECK (id > 0)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT NOT NULL,
            first_name VARCHAR(255) NOT NULL,
            CONSTRAINT chk_mytable_id CHECK (id >= 1)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(2, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` DROP CONSTRAINT `chk_mytable_id`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        var checkCondition = "`id` >= 1";
        Assert.Equal($"ALTER TABLE `mytable` ADD CONSTRAINT `chk_mytable_id` CHECK ({checkCondition})", second.ToSql());
    }

    [Fact]
    public void MultipleReorders()
    {
        var startText = """
        CREATE TABLE interfaces (
            id INT NOT NULL,
            interface VARCHAR(100) NOT NULL,
            mac_address VARCHAR(17) NOT NULL,
            lan_type VARCHAR(50) NOT NULL,
            vlan VARCHAR(10) NOT NULL,
            net_ip VARCHAR(15) NOT NULL,
            netmask VARCHAR(15) NOT NULL,
            gateway VARCHAR(15) NOT NULL,
            remarks VARCHAR(255) NULL,
            last_mod_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP(),
            last_mod_by VARCHAR(100) NOT NULL,
            PRIMARY KEY (id)
        )
        """;

        var endText = """
        CREATE TABLE interfaces (
            id INT NOT NULL,
            interface VARCHAR(100) NOT NULL,
            host_ip VARCHAR(15) NOT NULL,
            netmask VARCHAR(15) NOT NULL,
            ip_offset VARCHAR(10) NOT NULL,
            vlan VARCHAR(10) NOT NULL,
            hardware_mac_address VARCHAR(17) NOT NULL,
            software_mac_address VARCHAR(17) NOT NULL,
            server_nic_id VARCHAR(50) NOT NULL,
            switch_hostname VARCHAR(100) NOT NULL,
            remarks VARCHAR(255) NULL,
            last_mod_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP(),
            last_mod_by VARCHAR(100) NOT NULL,
            PRIMARY KEY (id)
        )
        """;

        var changes = ComputeChanges(startText, endText);
        var change = Assert.Single(changes);

        var alterTable = change.Statement as AlterTable;
        Assert.NotNull(alterTable);
        var operations = alterTable.Operations;
        Assert.Equal(12, operations.Count);

        var expectedOperations = new List<(string, string?)>
        {
            ("ADD COLUMN `host_ip`", "`interface`"),
            ("MODIFY COLUMN `netmask`", "`host_ip`"),
            ("ADD COLUMN `ip_offset`", "`netmask`"),
            ("MODIFY COLUMN `vlan`", "`ip_offset`"),
            ("ADD COLUMN `hardware_mac_address`", "`vlan`"),
            ("ADD COLUMN `software_mac_address`", "`hardware_mac_address`"),
            ("ADD COLUMN `server_nic_id`", "`software_mac_address`"),
            ("ADD COLUMN `switch_hostname`", "`server_nic_id`"),
            ("DROP COLUMN `mac_address`", null),
            ("DROP COLUMN `lan_type`", null),
            ("DROP COLUMN `net_ip`", null),
            ("DROP COLUMN `gateway`", null),
        };

        for (int i = 0; i < expectedOperations.Count; i++)
        {
            var expectedOperation = expectedOperations[i];
            var actualSql = operations[i].ToSql();
            Assert.StartsWith(expectedOperation.Item1, actualSql);
            if (expectedOperation.Item2 != null)
            {
                Assert.EndsWith($"AFTER {expectedOperation.Item2}", actualSql);
            }
        }
    }

    [Fact]
    public void ChangeSpatialIndex()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT,
            area POLYGON,
            PRIMARY KEY (id),
            SPATIAL INDEX idx_area (area) COMMENT 'Area index'
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT,
            area POLYGON,
            PRIMARY KEY (id),
            SPATIAL INDEX idx_area (area) COMMENT 'Updated area index'
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(2, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` DROP KEY `idx_area`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD SPATIAL KEY `idx_area` (`area`) COMMENT 'Updated area index'", second.ToSql());
    }

    [Fact]
    public void ChangeFunctionalIndex()
    {
        var startText = """
        CREATE TABLE mytable (
            id INT,
            name VARCHAR(100),
            PRIMARY KEY (id),
            INDEX idx_name_lower ((LOWER(name)) DESC)
        );
        """;

        var endText = """
        CREATE TABLE mytable (
            id INT,
            name VARCHAR(100),
            PRIMARY KEY (id),
            INDEX idx_name_lower ((LOWER(name)) ASC)
        );
        """;

        var changes = ComputeChanges(startText, endText);
        Assert.Equal(2, changes.Count);

        var first = changes[0].Statement as AlterTable;
        Assert.NotNull(first);
        Assert.Single(first.Operations);
        Assert.Equal("ALTER TABLE `mytable` DROP KEY `idx_name_lower`", first.ToSql());

        var second = changes[1].Statement as AlterTable;
        Assert.NotNull(second);
        Assert.Single(second.Operations);
        Assert.Equal("ALTER TABLE `mytable` ADD KEY `idx_name_lower` ((LOWER(`name`)))", second.ToSql());
    }
}
