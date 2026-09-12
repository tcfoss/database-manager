using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using GenerationMode = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.GenerationMode;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class TableParser
{
    private readonly Parser _p;
    private readonly Func<ParserState, Identifier> _ident;
    private readonly Func<ParserState, ObjectName> _objname;

    public TableParser(Parser parser)
    {
        _p = parser;
        _ident = _p.ComponentParser.ParseIdentifier;
        _objname = _p.ComponentParser.ParseObjectName;
    }


    public CreateTable ParseCreate(ParserState state, bool orReplace, bool temporary)
    {
        state.ExpectKeyword(Keyword.TABLE);

        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _objname(state);

        (SqlValueList<StatementColumn> columns, SqlValueList<StatementTableConstraint> constraints) = ParseColumns(state);

        SqlValueList<StatementTableOption> options = ParseTableOptions(state);
        (bool? printAs, Select? selectStatement) = ParseAsSelect(state);

        return new CreateTable(name, columns, constraints)
        {
            OrReplace = orReplace,
            Temporary = temporary,
            IfNotExists = ifNotExists,
            TableOptions = options,
            PrintAs = printAs,
            AsSelect = selectStatement
        };
    }

    private (SqlValueList<StatementColumn>, SqlValueList<StatementTableConstraint>) ParseColumns(ParserState state)
    {
        var columns = new SqlValueList<StatementColumn>();
        var constraints = new SqlValueList<StatementTableConstraint>();

        if (!state.ConsumeTokenIs<ParenOpen>() || state.ConsumeTokenIs<ParenClose>())
        {
            return (columns, constraints);
        }

        while (true)
        {
            StatementTableConstraint? currentConstraint;
            if ((currentConstraint = ParseOptionalTableConstraint(state)) != null)
            {
                constraints.Add(currentConstraint);
            }
            else if (state.PeekIs<Word>())
            {
                columns.Add(ParseColumn(state));
            }
            else
            {
                throw state.ExpectedException("column_spec".Italic(), "key_spec".Italic(), "constraint_spec".Italic());
            }

            bool commaFound = state.ConsumeTokenIs<Comma>();
            bool rightParenFound = state.PeekIs<ParenClose>();

            if (!commaFound && !rightParenFound)
            {
                throw state.ExpectedException(",", ")");
            }

            if (rightParenFound)
            {
                state.ConsumeTokenIs<ParenClose>();
                break;
            }
        }

        return (columns, constraints);
    }

    private StatementColumn ParseColumn(ParserState state)
    {
        Identifier name = _ident(state);
        DataType dataType = _p.DataTypeParser.ParseDataType(state);

        SqlValueList<StatementColumnOption> options = [];
        HashSet<string> usedKeys = [];

        while (true)
        {
            Location currentLocation = state.Peek().Location;
            StatementColumnOption? curr = ParseNextColumnOption(state);
            if (curr == null)
            {
                break;
            }

            if (!usedKeys.Add(curr.DuplicateCheckKey))
            {
                throw new ParseException.Duplicate(curr.DuplicateCheckKey, currentLocation);
            }

            options.Add(curr);
        }

        return new StatementColumn(name, dataType, options);
    }

    public StatementColumnOption? ParseNextColumnOption(ParserState state)
    {
        Identifier? constraintName = null;
        Location? constraintLocation = null;
        if (state.ParseKeyword(Keyword.CONSTRAINT))
        {
            constraintLocation = state.PeekNth(-1).Location;
            constraintName = _ident(state);
        }

        if (state.ParseKeywordsAll(Keyword.NOT, Keyword.NULL))
        {
            return new StatementColumnOption.Nullability.NotNull(constraintName);
        }
        if (state.ParseKeyword(Keyword.NULL))
        {
            return new StatementColumnOption.Nullability.Null(constraintName);
        }
        if (state.ParseKeyword(Keyword.DEFAULT))
        {
            return ParseDefault(state, constraintName);
        }
        if (state.ParseKeyword(Keyword.CHECK))
        {
            return new StatementColumnOption.CheckConstraint(state.ParseParenthesized(_p.ExpressionParser.ParseExpr), constraintName);
        }
        if (state.ParseKeywordsAll(Keyword.PRIMARY, Keyword.KEY))
        {
            return new StatementColumnOption.PrimaryKey()
            {
                Name = constraintName
            };
        }
        if (state.ParseKeyword(Keyword.UNIQUE))
        {
            return new StatementColumnOption.Unique()
            {
                Name = constraintName
            };
        }
        if (state.ParseKeywordsAll(Keyword.GENERATED, Keyword.ALWAYS, Keyword.AS))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("GENERATED", constraintName, constraintLocation);
            }
            return ParseGenerated(state, true);
        }
        if (state.ParseKeyword(Keyword.AS))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("GENERATED", constraintName, constraintLocation);
            }
            return ParseGenerated(state, false);
        }
        if (state.ParseKeyword(Keyword.COMMENT))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("COMMENT", constraintName, constraintLocation);
            }
            bool hasEquals = state.ConsumeTokenIs<Equal>();
            string commentValue = ValueParser.ParseLiteralString(state);
            return hasEquals ? new StatementColumnOption.ColumnComment(new Comment.WithEqual(commentValue)) : new StatementColumnOption.ColumnComment(new Comment(commentValue));
        }
        if (state.ParseKeyword(Keyword.IDENTITY))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("IDENTITY", constraintName, constraintLocation);
            }
            return ParseAfterIdentity(state);
        }
        if (state.ParseKeyword(Keyword.AUTO_INCREMENT))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("AUTO_INCREMENT", constraintName, constraintLocation);
            }
            return new StatementColumnOption.AutoIncrement();
        }
        if (state.ParseKeywordsAll(Keyword.ON, Keyword.UPDATE))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("ON UPDATE", constraintName, constraintLocation);
            }
            Expression expr = _p.ExpressionParser.ParseExpr(state);
            if (expr is Nested nested)
            {
                return new StatementColumnOption.OnUpdate(nested.Expression);
            }
            return new StatementColumnOption.OnUpdate(expr);
        }
        if (constraintName != null)
        {
            throw state.ExpectedCategoryException("constraint_definition");
        }

        return null;
    }

    private StatementColumnOption.Default ParseDefault(ParserState state, Identifier? constraintName)
    {
        if (state.Peek() is NumericLiteral or StringLiteral or NationalStringLiteral or HexStringLiteral
                or Word { Keyword: Keyword.TRUE or Keyword.FALSE or Keyword.NULL }
            && state.TryParse(ValueParser.ParseValue, out Value? val))
        {
            return new StatementColumnOption.Default.DefaultValue(val, constraintName);
        }

        return state.PeekIs<ParenOpen>()
            ? new StatementColumnOption.Default.DefaultExpression(state.ParseParenthesized(_p.ExpressionParser.ParseExpr), constraintName)
            : new StatementColumnOption.Default.DefaultExpression(_p.ExpressionParser.ParseExpr(state), constraintName);
    }

    private static GenerationMode? ParseGenerationMode(Keyword? generationModeKeyword)
    {
        if (generationModeKeyword == Keyword.VIRTUAL)
        {
            return GenerationMode.Virtual;
        }
        if (generationModeKeyword is Keyword.STORED or Keyword.PERSISTENT)
        {
            return GenerationMode.Stored;
        }
        return null;
    }

    private StatementColumnOption.Generated.AsExpression ParseGenerated(ParserState state, bool writeGeneratedAlways)
    {
        Expression genExpr = state.ParseParenthesized(_p.ExpressionParser.ParseExpr);
        Keyword genModeKeyword = state.ParseKeywordsAny(Keyword.VIRTUAL, Keyword.STORED, Keyword.PERSISTENT);
        GenerationMode? genMode = ParseGenerationMode(genModeKeyword);
        bool storedAsPersistent = genModeKeyword == Keyword.PERSISTENT;

        var newVal = new StatementColumnOption.Generated.AsExpression(
            genExpr,
            genMode ?? GenerationMode.Virtual,
            writeGeneratedAlways,
            genMode != null,
            storedAsPersistent);

        return newVal;
    }

    private static StatementColumnOption.Identity ParseAfterIdentity(ParserState state)
    {
        if (!state.PeekIs<ParenOpen>())
        {
            return new StatementColumnOption.Identity();
        }

        if (!state.TryParse(s => s.ParseParenthesizedCommaSeparated(ValueParser.ParseLiteralULong, false), out SqlValueList<ulong>? values))
        {
            return new StatementColumnOption.Identity();
        }

        if (values.Count != 2)
        {
            throw state.ExpectedException("(seed, increment)");
        }

        return new StatementColumnOption.Identity.Specified(values[0], values[1]);
    }

    public StatementTableConstraint? ParseOptionalTableConstraint(ParserState state)
    {
        Identifier? name = state.ParseInit(state.ParseKeyword(Keyword.CONSTRAINT), _p.ComponentParser.ParseIdentifier);

        Token token = state.Peek();

        return token switch
        {
            Word { Keyword: Keyword.PRIMARY } => ParsePrimary(state, name),
            Word { Keyword: Keyword.UNIQUE } => ParseUnique(state, name),
            Word { Keyword: Keyword.FOREIGN } => ParseForeign(state, name),
            Word { Keyword: Keyword.CHECK } => ParseTableCheck(state, name),
            Word { Keyword: Keyword.INDEX or Keyword.KEY } => ParseIndex(state),
            Word { Keyword: Keyword.FULLTEXT } => ParseFullText(state),
            Word { Keyword: Keyword.SPATIAL } => ParseSpatial(state),
            _ => null
        };
    }

    private StatementTableConstraint.PrimaryKey ParsePrimary(ParserState state, Identifier? name)
    {
        state.ExpectKeywordsAll(Keyword.PRIMARY, Keyword.KEY);

        IndexOrganization? indexOrganization = ParseOptionalIndexOrganization(state);
        IndexMethod? method = null;
        if (state.Peek() is Word { Keyword: Keyword.USING })
        {
            state.Next();
            method = ParseIndexMethod(state);
        }
        SqlValueList<KeyPart> cols = _p.ComponentParser.ParseParenthesizedKeyPartList(state, false);
        if (state.Peek() is Word { Keyword: Keyword.USING })
        {
            if (method != null)
            {
                throw new ParseException.Duplicate("index_method".Italic(), state.PeekNth(-1).Location);
            }
            state.Next();
            method = ParseIndexMethod(state);
        }

        SqlValueList<StatementIndexOption>? indexOptions = null;
        if (state.PeekKeyword(Keyword.WITH))
        {
            indexOptions = ParseIndexOptions(state);
        }

        IndexStorageLocation? storageLocation = ParseOptionalIndexStorageLocation(state);

        Comment? comment = ComponentParser.ParseComment(state);

        return new StatementTableConstraint.PrimaryKey(cols, name)
        {
            IndexMethod = method,
            Comment = comment,
            IndexOrganization = indexOrganization,
            Options = indexOptions,
            StorageLocation = storageLocation
        };
    }

    private StatementTableConstraint.UniqueConstraint ParseUnique(ParserState state, Identifier? constraintName)
    {
        state.ExpectKeyword(Keyword.UNIQUE);

        IndexOrganization? indexOrganization = ParseOptionalIndexOrganization(state);

        KeyLabel? label = state.Peek() switch
        {
            Word { Keyword: Keyword.KEY } => KeyLabel.Key,
            Word { Keyword: Keyword.INDEX } => KeyLabel.Index,
            _ => null,
        };

        if (label != null)
        {
            state.Next();
        }

        Identifier? indexName = null;
        IndexMethod? method = null;
        Comment? comment = null;
        SqlValueList<KeyPart>? columns = null;
        while (true)
        {
            Location currentLocation = state.Peek().Location;
            if (state.PeekKeyword(Keyword.USING))
            {
                if (method != null)
                {
                    throw new ParseException.Duplicate("index_method".Italic(), currentLocation);
                }
                state.Next();
                method = ParseIndexMethod(state);
            }

            else if (state.PeekKeyword(Keyword.COMMENT))
            {
                if (comment != null)
                {
                    throw new ParseException.Duplicate("comment".Italic(), currentLocation);
                }
                comment = ComponentParser.ParseComment(state);
            }
            else if (state.PeekIs<ParenOpen>())
            {
                if (columns != null)
                {
                    throw new ParseException.Duplicate("key_columns".Italic(), currentLocation);
                }
                columns = _p.ComponentParser.ParseParenthesizedKeyPartList(state, false);
            }
            else if (state.Peek() is Word word && (word.Keyword == Keyword.undefined || word.QuoteStyle is not QuoteStyle.None))
            {
                indexName = _p.ComponentParser.ParseIdentifier(state);
            }
            else
            {
                break;
            }
        }

        if (columns == null)
        {
            throw state.ExpectedCategoryException("column_list");
        }

        IncludedColumns? includedColumns = ParseOptionalIncludedColumns(state);
        SqlValueList<StatementIndexOption>? indexOptions = null;
        if (state.PeekKeyword(Keyword.WITH))
        {
            indexOptions = ParseIndexOptions(state);
        }
        IndexStorageLocation? storageLocation = ParseOptionalIndexStorageLocation(state);

        return new StatementTableConstraint.UniqueConstraint(columns, constraintName)
        {
            IndexMethod = method,
            IndexOrganization = indexOrganization,
            Comment = comment,
            IndexName = indexName,
            KeyLabel = label,
            IncludedColumns = includedColumns,
            Options = indexOptions,
            StorageLocation = storageLocation
        };
    }

    private StatementTableConstraint.ForeignKey ParseForeign(ParserState state, Identifier? name)
    {
        state.ExpectKeywordsAll(Keyword.FOREIGN, Keyword.KEY);
        SqlValueList<Identifier> cols = _p.ComponentParser.ParseParenthisizedIdentifierList(state, false);

        state.ExpectKeyword(Keyword.REFERENCES);

        ObjectName foreignTable = _objname(state);

        SqlValueList<Identifier> foreignCols = _p.ComponentParser.ParseParenthisizedIdentifierList(state, false);

        ReferentialAction? onDelete = null;
        ReferentialAction? onUpdate = null;
        while (true)
        {
            if (state.ParseKeywordsAll(Keyword.ON, Keyword.DELETE))
            {
                onDelete = ParseReferentialAction(state);
            }
            else if (state.ParseKeywordsAll(Keyword.ON, Keyword.UPDATE))
            {
                onUpdate = ParseReferentialAction(state);
            }
            else
            {
                break;
            }
        }

        Comment? comment = null;
        if (state.PeekKeyword(Keyword.COMMENT))
        {
            comment = ComponentParser.ParseComment(state);
        }

        return new StatementTableConstraint.ForeignKey(cols, foreignTable, foreignCols, name)
        {
            OnDelete = onDelete,
            OnUpdate = onUpdate,
            Comment = comment,
        };
    }

    public static ReferentialAction ParseReferentialAction(ParserState state)
    {
        if (state.ParseKeyword(Keyword.RESTRICT))
        {
            return ReferentialAction.Restrict;
        }
        if (state.ParseKeyword(Keyword.CASCADE))
        {
            return ReferentialAction.Cascade;
        }
        if (state.ParseKeywordsAll(Keyword.SET, Keyword.NULL))
        {
            return ReferentialAction.SetNull;
        }
        if (state.ParseKeywordsAll(Keyword.SET, Keyword.DEFAULT))
        {
            return ReferentialAction.SetDefault;
        }
        if (state.ParseKeywordsAll(Keyword.NO, Keyword.ACTION))
        {
            return ReferentialAction.NoAction;
        }
        throw state.ExpectedException<ReferentialAction>();
    }

    private StatementTableConstraint.Check ParseTableCheck(ParserState state, Identifier? name)
    {
        state.ExpectKeyword(Keyword.CHECK);
        Expression expr = state.ParseParenthesized(_p.ExpressionParser.ParseExpr);
        Comment? comment = ComponentParser.ParseComment(state);
        return new StatementTableConstraint.Check(expr, name)
        {
            Comment = comment,
        };
    }

    private static KeyLabel? ParseOptionalKeyLabel(ParserState state)
    {
        Keyword keyLabelKeyword = state.ParseKeywordsAny(Keyword.KEY, Keyword.INDEX);
        if (keyLabelKeyword == Keyword.KEY)
        {
            return KeyLabel.Key;
        }
        if (keyLabelKeyword == Keyword.INDEX)
        {
            return KeyLabel.Index;
        }
        return null;
    }

    private static KeyLabel ParseKeyLabel(ParserState state)
    {
        KeyLabel keyLabel = ParseOptionalKeyLabel(state) ?? throw state.ExpectedException("KEY", "INDEX");
        return keyLabel;
    }

    protected virtual StatementTableConstraint ParseIndex(ParserState state)
    {
        KeyLabel label = ParseKeyLabel(state);

        Identifier? name = null;
        IndexMethod? method = null;
        IndexOrganization? indexOrganization = null;
        bool unique = false;

        while (true)
        {
            Token next = state.Next();

            if (next is Word { Keyword: Keyword.USING })
            {
                if (method != null)
                {
                    throw new ParseException.Duplicate("index_method".Italic(), next.Location);
                }
                method = ParseIndexMethod(state);
            }
            else if (next is Word { Keyword: Keyword.UNIQUE })
            {
                if (unique)
                {
                    throw new ParseException.Duplicate("UNIQUE", next.Location);
                }
                unique = true;
            }
            else if (next is Word { Keyword: Keyword.CLUSTERED or Keyword.NONCLUSTERED })
            {
                state.Rewind();
                indexOrganization = ParseOptionalIndexOrganization(state);
            }
            else if (next is Word w)
            {
                if (name != null)
                {
                    throw new ParseException.Duplicate("index_name".Italic(), next.Location);
                }
                name = w.ToIdentifier(state.SourceId);
            }
            else
            {
                state.Rewind();
                break;
            }
        }

        SqlValueList<KeyPart> cols = _p.ComponentParser.ParseParenthesizedKeyPartList(state, false);

        IncludedColumns? includedColumns = ParseOptionalIncludedColumns(state);

        Expression? filter = null;
        if (state.ParseKeyword(Keyword.WHERE))
        {
            filter = _p.ExpressionParser.ParseExpr(state);
        }

        SqlValueList<StatementIndexOption>? indexOptions = null;
        if (state.PeekKeyword(Keyword.WITH))
        {
            indexOptions = ParseIndexOptions(state);
        }

        IndexStorageLocation? storageLocation = ParseOptionalIndexStorageLocation(state);

        if (state.PeekKeyword(Keyword.USING))
        {
            if (method != null)
            {
                throw new ParseException.Duplicate("index_method".Italic(), state.Peek().Location);
            }
            state.Next();
            method = ParseIndexMethod(state);
        }

        Comment? comment = null;
        if (state.PeekKeyword(Keyword.COMMENT))
        {
            comment = ComponentParser.ParseComment(state);
        }

        if (unique)
        {
            return new StatementTableConstraint.UniqueIndex(cols, name)
            {
                IndexMethod = method,
                Comment = comment,
                KeyLabel = label,
                IndexOrganization = indexOrganization,
                IncludedColumns = includedColumns,
                Filter = filter,
                Options = indexOptions,
                StorageLocation = storageLocation
            };
        }
        return new StatementTableConstraint.Standard(cols, name)
        {
            IndexMethod = method,
            Comment = comment,
            KeyLabel = label,
            IndexOrganization = indexOrganization,
            IncludedColumns = includedColumns,
            Filter = filter,
            Options = indexOptions,
            StorageLocation = storageLocation
        };
    }

    public virtual IndexMethod ParseIndexMethod(ParserState state)
    {
        Token token = state.Next();
        return token switch
        {
            Word { Keyword: Keyword.BTREE } => IndexMethod.Btree,
            Word { Keyword: Keyword.HASH } => IndexMethod.Hash,
            Word { Keyword: Keyword.GIST } => IndexMethod.Gist,
            Word { Keyword: Keyword.SPGIST } => IndexMethod.SpGist,
            Word { Keyword: Keyword.GIN } => IndexMethod.Gin,
            Word { Keyword: Keyword.BRIN } => IndexMethod.Brin,
            Word { Keyword: Keyword.RTREE } => IndexMethod.Rtree,
            _ => throw state.ExpectedException<IndexMethod>(token)
        };
    }

    private StatementTableConstraint.Spatial ParseSpatial(ParserState state)
    {
        state.ExpectKeyword(Keyword.SPATIAL);

        KeyLabel? label = ParseOptionalKeyLabel(state);

        Identifier? name = null;
        if (state.Peek() is Word w)
        {
            name = w.ToIdentifier(state.SourceId);
            state.Next();
        }

        SqlValueList<KeyPart> cols = _p.ComponentParser.ParseParenthesizedKeyPartList(state, false);

        Comment? comment = null;
        if (state.PeekKeyword(Keyword.COMMENT))
        {
            comment = ComponentParser.ParseComment(state);
        }

        return new StatementTableConstraint.Spatial(cols, name)
        {
            Comment = comment,
            KeyLabel = label,
        };
    }

    private StatementTableConstraint.FullText ParseFullText(ParserState state)
    {
        state.ExpectKeyword(Keyword.FULLTEXT);
        KeyLabel? label = ParseOptionalKeyLabel(state);

        Identifier? name = null;
        if (state.Peek() is Word w)
        {
            name = w.ToIdentifier(state.SourceId);
            _ = state.Next();
        }

        SqlValueList<KeyPart> cols = _p.ComponentParser.ParseParenthesizedKeyPartList(state, false);

        Comment? comment = null;
        if (state.PeekKeyword(Keyword.COMMENT))
        {
            comment = ComponentParser.ParseComment(state);
        }

        return new StatementTableConstraint.FullText(cols, name)
        {
            Comment = comment,
            KeyLabel = label,
        };
    }

    public virtual IndexOrganization? ParseOptionalIndexOrganization(ParserState state)
    {
        if (state.ParseKeyword(Keyword.CLUSTERED))
        {
            return IndexOrganization.Clustered;
        }
        if (state.ParseKeyword(Keyword.NONCLUSTERED))
        {
            return IndexOrganization.Nonclustered;
        }
        return null;
    }

    public virtual IncludedColumns? ParseOptionalIncludedColumns(ParserState state)
    {
        if (state.ParseKeyword(Keyword.INCLUDE))
        {
            SqlValueList<Identifier> columns = _p.ComponentParser.ParseParenthisizedIdentifierList(state, false);
            return new IncludedColumns(columns);
        }
        return null;
    }

    public virtual SqlValueList<StatementIndexOption> ParseIndexOptions(ParserState state)
    {
        state.ExpectKeyword(Keyword.WITH);
        state.ExpectToken<ParenOpen>();

        var options = new SqlValueList<StatementIndexOption>();
        var usedKeys = new HashSet<string>();

        while (true)
        {
            Location currentLocation = state.Peek().Location;
            StatementIndexOption curr = ParseNextIndexOption(state) ?? throw state.ExpectedException("PAD_INDEX", "FILLFACTOR", "IGNORE_DUP_KEY", "STATISTICS_NORECOMPUTE", "STATISTICS_INCREMENTAL", "ALLOW_ROW_LOCKS", "ALLOW_PAGE_LOCKS", "OPTIMIZE_FOR_SEQUENTIAL_KEY");
            if (!usedKeys.Add(GetIndexOptionDuplicateKey(curr)))
            {
                throw new ParseException.Duplicate("index_option".Italic(), currentLocation);
            }

            options.Add(curr);

            if (!state.ConsumeTokenIs<Comma>())
            {
                break;
            }
        }

        state.ExpectToken<ParenClose>();

        return options;
    }

    private static string GetIndexOptionDuplicateKey(StatementIndexOption option) => option.GetType().Name;

    public virtual StatementIndexOption? ParseNextIndexOption(ParserState state)
    {
        if (state.ParseKeyword(Keyword.PAD_INDEX))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.PadIndex(ParseIsOn(state));
        }
        if (state.ParseKeyword(Keyword.FILLFACTOR))
        {
            state.ExpectToken<Equal>();
            uint fillFactor = ValueParser.ParseLiteralUInt(state);
            return new StatementIndexOption.FillFactor(fillFactor);
        }

        if (state.ParseKeyword(Keyword.IGNORE_DUP_KEY))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.IgnoreDupKey(ParseIsOn(state));
        }
        if (state.ParseKeyword(Keyword.STATISTICS_NORECOMPUTE))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.StatisticsNoRecompute(ParseIsOn(state));
        }
        if (state.ParseKeyword(Keyword.STATISTICS_INCREMENTAL))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.StatisticsIncremental(ParseIsOn(state));
        }
        if (state.ParseKeyword(Keyword.ALLOW_ROW_LOCKS))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.AllowRowLocks(ParseIsOn(state));
        }
        if (state.ParseKeyword(Keyword.ALLOW_PAGE_LOCKS))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.AllowPageLocks(ParseIsOn(state));
        }
        if (state.ParseKeyword(Keyword.OPTIMIZE_FOR_SEQUENTIAL_KEY))
        {
            state.ExpectToken<Equal>();
            return new StatementIndexOption.OptimizeForSequentialKey(ParseIsOn(state));
        }

        return null;
    }

    private static bool ParseIsOn(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ON))
        {
            return true;
        }
        if (state.ParseKeyword(Keyword.OFF))
        {
            return false;
        }

        throw state.ExpectedException("ON", "OFF");
    }

    public IndexStorageLocation? ParseOptionalIndexStorageLocation(ParserState state)
    {
        if (state.ParseKeyword(Keyword.ON))
        {
            Identifier partitionSchemeOrFileGroup = _ident(state);
            if (state.ConsumeTokenIs<ParenOpen>())
            {
                Identifier column = _ident(state);
                state.ExpectToken<ParenClose>();
                return new IndexStorageLocation.PartitionScheme(partitionSchemeOrFileGroup, column);
            }
            return new IndexStorageLocation.Filegroup(partitionSchemeOrFileGroup);
        }
        return null;
    }

    private SqlValueList<StatementTableOption> ParseTableOptions(ParserState state)
    {
        var options = new SqlValueList<StatementTableOption>();
        var usedKeys = new HashSet<string>();

        while (true)
        {
            Location currentLocation = state.Peek().Location;
            StatementTableOption? curr = ParseNextTableOption(state);
            if (curr == null)
            {
                break;
            }

            if (curr.DuplicateCheckKey != null && !usedKeys.Add(curr.DuplicateCheckKey))
            {
                throw new ParseException.Duplicate(curr.DuplicateCheckKey, currentLocation);
            }
            options.Add(curr);
        }
        return options;
    }

    protected virtual StatementTableOption? ParseNextTableOption(ParserState state)
    {
        return null;
    }

    private (bool?, Select?) ParseAsSelect(ParserState state)
    {
        bool printAs = state.ParseKeyword(Keyword.AS);

        if (state.PeekKeyword(Keyword.SELECT))
        {
            Select select = _p.SelectParser.ParseSelect(state, null);

            return (printAs, select);
        }

        return (null, null);
    }

    public AlterTable ParseAlter(ParserState state)
    {
        state.ExpectKeyword(Keyword.TABLE);

        ObjectName tableName = _objname(state);

        var operations = new SqlValueList<AlterTableOperation>();

        while (true)
        {
            operations.Add(ParseNextAlterOperation(state));

            bool commaFound = state.ConsumeTokenIs<Comma>();
            bool statementEnd = state.PeekIs<Semicolon>() || state.PeekIs<Eof>();

            if (!commaFound && !statementEnd)
            {
                throw state.ExpectedException(",", ";");
            }
            if (statementEnd)
            {
                break;
            }
        }

        return new AlterTable(tableName, operations);
    }

    private AlterTableOperation ParseNextAlterOperation(ParserState state)
    {
        Word next = state.Next() as Word ?? throw ParserState.ExpectedCategoryException("alter_table_operation", state.PeekNth(-1));
        Keyword nextKeyword = next.Keyword;

        if (nextKeyword == Keyword.AUTO_INCREMENT)
        {
            state.ConsumeTokenIs<Equal>();
            ulong value = ValueParser.ParseLiteralULong(state);
            return new AlterTableOperation.AutoIncrement(value);
        }
        if (nextKeyword == Keyword.ADD)
        {
            AlterTableOperation? operation = SubParseAlterAddConstraint(state);

            if (operation != null)
            {
                return operation;
            }

            return SubParseAlterAddColumn(state);
        }
        if (nextKeyword == Keyword.MODIFY)
        {
            _ = state.ParseKeyword(Keyword.COLUMN);

            StatementColumn column = ParseColumn(state);
            return new AlterTableOperation.ModifyColumn(column)
            {
                Position = ParseAlterTableColumnPosition(state)
            };
        }
        if (nextKeyword == Keyword.CHANGE)
        {
            _ = state.ParseKeyword(Keyword.COLUMN);

            Identifier oldName = _ident(state);
            StatementColumn newColumn = ParseColumn(state);

            return new AlterTableOperation.ChangeColumn(oldName, newColumn)
            {
                Position = ParseAlterTableColumnPosition(state)
            };
        }
        if (nextKeyword == Keyword.ALTER)
        {
            // CHECK ENFORCED | NOT ENFORCED
            // INDEX VISIBLE | INVISIBLE
            // COLUMN VISIBLE | INVISIBLE
            _ = state.ParseKeyword(Keyword.COLUMN);
            Identifier columnName = _ident(state);
            if (state.ParseKeywordsAll(Keyword.SET, Keyword.DEFAULT))
            {
                StatementColumnOption.Default newDefault = ParseDefault(state, null);
                return new AlterTableOperation.SetDefault(columnName, newDefault);
            }
            if (state.ParseKeywordsAll(Keyword.DROP, Keyword.DEFAULT))
            {
                return new AlterTableOperation.DropDefault(columnName);
            }
        }
        if (nextKeyword == Keyword.DROP)
        {
            return SubParseDrop(state);
        }
        if (nextKeyword == Keyword.RENAME)
        {
            if (state.ParseKeyword(Keyword.COLUMN))
            {
                Identifier oldColumnName = _ident(state);
                state.ExpectKeyword(Keyword.TO);
                Identifier newColumnName = _ident(state);
                return new AlterTableOperation.RenameColumn(oldColumnName, newColumnName);
            }
            _ = state.ParseKeywordsAny(Keyword.TO, Keyword.AS);
            ObjectName newName = _objname(state);
            return new AlterTableOperation.Rename(newName);
        }

        throw ParserState.ExpectedCategoryException("alter_table_operation", next);
    }

    private AlterTableOperation? SubParseAlterAddConstraint(ParserState state)
    {
        StatementTableConstraint? currentConstraint = ParseOptionalTableConstraint(state);
        return currentConstraint switch
        {
            StatementTableConstraint.PrimaryKey pk =>
                new AlterTableOperation.AddPrimaryKey(pk),
            StatementTableConstraint.UniqueConstraint uk =>
                new AlterTableOperation.AddUniqueKey(uk),
            StatementTableConstraint.ForeignKey fk =>
                new AlterTableOperation.AddForeignKey(fk),
            StatementTableConstraint.Check check =>
                new AlterTableOperation.AddCheck(check),
            StatementTableConstraint.Standard standardKey =>
                new AlterTableOperation.AddStandardKey(standardKey),
            StatementTableConstraint.FullText fullTextKey =>
                new AlterTableOperation.AddFullTextKey(fullTextKey),
            StatementTableConstraint.Spatial spatialKey =>
                new AlterTableOperation.AddSpatialKey(spatialKey),
            _ => null
        };
    }

    private AlterTableOperation.AddColumn SubParseAlterAddColumn(ParserState state)
    {
        state.ParseKeyword(Keyword.COLUMN);

        StatementColumn column = ParseColumn(state);
        return new AlterTableOperation.AddColumn(column)
        {
            Position = ParseAlterTableColumnPosition(state)
        };
    }

    private AlterTableOperation SubParseDrop(ParserState state)
    {
        if (state.ParseKeyword(Keyword.PRIMARY))
        {
            state.ExpectKeyword(Keyword.KEY);
            if (state.PeekIs<Word>())
            {
                Identifier pkName = _ident(state);
                return new AlterTableOperation.DropPrimaryKey(pkName);
            }
            return new AlterTableOperation.DropPrimaryKey();
        }
        if (state.ParseKeyword(Keyword.FOREIGN))
        {
            state.ExpectKeyword(Keyword.KEY);
            Identifier fkName = _ident(state);
            return new AlterTableOperation.DropForeignKey(fkName);
        }
        if (state.ParseKeywordsAny(Keyword.INDEX, Keyword.KEY) != Keyword.undefined)
        {
            Identifier indexName = _ident(state);
            return new AlterTableOperation.DropKey(indexName);
        }
        if (state.ParseKeyword(Keyword.CHECK))
        {
            Identifier checkName = _ident(state);
            return new AlterTableOperation.DropCheck(checkName);
        }
        if (state.ParseKeyword(Keyword.CONSTRAINT))
        {
            Identifier constraintName = _ident(state);
            return new AlterTableOperation.DropConstraint(constraintName);
        }

        state.ParseKeyword(Keyword.COLUMN);
        Identifier columnName = _ident(state);
        return new AlterTableOperation.DropColumn(columnName);
    }


    private AlterTableColumnPosition? ParseAlterTableColumnPosition(ParserState state)
    {
        if (state.ParseKeyword(Keyword.FIRST))
        {
            return new AlterTableColumnPosition.First();
        }
        else if (state.ParseKeyword(Keyword.AFTER))
        {
            Identifier afterColumn = _ident(state);
            return new AlterTableColumnPosition.AfterColumn(afterColumn);
        }
        return null;
    }
}
