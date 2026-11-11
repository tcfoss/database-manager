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
    protected virtual bool SupportsPartitionAsSelect => true;

    private readonly Parser _p;
    private readonly Func<ParserState, Identifier> _ident;
    private readonly Func<ParserState, ObjectName> _objname;
    private readonly Func<ParserState, string> _parseString = ValueParser.ParseLiteralStringOptionalQuotes;

    public TableParser(Parser parser)
    {
        _p = parser;
        _ident = _p.ComponentParser.ParseIdentifier;
        _objname = _p.ComponentParser.ParseObjectName;
    }


    public CreateTable ParseCreate(ParserState state, bool orReplace, TemporaryLabel? temporary, bool unlogged = false)
    {
        state.ExpectKeyword(Keyword.TABLE);

        bool ifNotExists = state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        ObjectName name = _objname(state);

        ObjectName? parent = _p.SupportsPostgresTableCreateSyntax && state.ParseKeywordsAll(Keyword.PARTITION, Keyword.OF)
            ? _objname(state)
            : null;

        bool printEmptyBody = state.PeekIs<ParenOpen>() && state.PeekNthIs<ParenClose>(1);

        (SqlValueList<StatementColumn> columns, SqlValueList<StatementTableConstraint> constraints) = ParseColumns(state, parent != null);

        PgPartitionOf? partitionOf = parent == null ? null : new PgPartitionOf(parent, ParsePartitionBound(state));
        PgPartitionBy? partitionBy = ParsePartitionBy(state);

        SqlValueList<StatementTableOption> options = ParseTableOptions(state);
        PgOnCommit? onCommit = ParseOnCommit(state);

        if (!SupportsPartitionAsSelect && (partitionOf != null || partitionBy != null)
            && state.Peek() is Word { Keyword: Keyword.AS or Keyword.SELECT })
        {
            throw state.ExpectedException("partition declaration without AS SELECT");
        }

        (bool printAs, Select? selectStatement) = ParseAsSelect(state);

        return new CreateTable(name, columns, constraints)
        {
            OrReplace = orReplace,
            Temporary = temporary,
            IfNotExists = ifNotExists,
            PgOptions = unlogged || onCommit != null || partitionOf != null || partitionBy != null
                ? new PgCreateTableOptions(unlogged, onCommit) { PartitionOf = partitionOf, PartitionBy = partitionBy }
                : null,
            PrintEmptyBody = printEmptyBody,
            TableOptions = options,
            PrintAs = printAs,
            AsSelect = selectStatement
        };
    }

    private (SqlValueList<StatementColumn>, SqlValueList<StatementTableConstraint>) ParseColumns(ParserState state, bool partition = false)
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
            else if (partition)
            {
                Identifier name = _ident(state);
                bool withOptions = state.ParseKeywordsAll(Keyword.WITH, Keyword.OPTIONS);
                columns.Add(new StatementColumn.PgPartitionOverride(name, ParseColumnOptions(state)) { WithOptions = withOptions });
            }
            else
            {
                columns.Add(ParseColumn(state));
            }


            if (state.ConsumeTokenIs<ParenClose>())
            {
                break;
            }
            if (state.ConsumeTokenIs<Comma>())
            {
                continue;
            }
            throw state.ExpectedException("','", "')'");
        }

        return (columns, constraints);
    }

    private StatementColumn ParseColumn(ParserState state)
    {
        Identifier name = _p.ComponentParser.ParseIdentifier(state, "column_name");

        StatementColumn.MsComputed? computedColumn = ParseOptionalComputedColumn(state, name);
        if (computedColumn != null)
        {
            return computedColumn;
        }

        DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);

        SqlValueList<StatementColumnOption> options = ParseColumnOptions(state);
        return new StatementColumn.Typed(name, dataType, options);
    }

    protected virtual StatementColumn.MsComputed? ParseOptionalComputedColumn(ParserState state, Identifier name)
    {
        if (!state.PeekKeyword(Keyword.AS))
        {
            return null;
        }

        state.Next(); // Consume the AS keyword
        Expression expr = _p.ExpressionParser.ParseExpr(state);

        MsComputedPersistenceMode? persistenceMode = null;
        if (state.ParseKeyword(Keyword.PERSISTED))
        {
            persistenceMode = state.ParseKeywordsAll(Keyword.NOT, Keyword.NULL) ? MsComputedPersistenceMode.PersistedNotNull : MsComputedPersistenceMode.Persisted;
        }

        SqlValueList<StatementColumnOption> options = ParseColumnOptions(state);

        return new StatementColumn.MsComputed(name, expr, options)
        {
            PersistenceMode = persistenceMode
        };
    }

    protected SqlValueList<StatementColumnOption> ParseColumnOptions(ParserState state)
    {
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

        return options;
    }

    public virtual StatementColumnOption? ParseNextColumnOption(ParserState state)
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
            using ParserState.Scope scope = state.EnterContext(ParseContext.DefaultExpression);
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
        if (state.ParseKeyword(Keyword.GENERATED))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("GENERATED", constraintName, constraintLocation);
            }
            bool always = state.ParseKeyword(Keyword.ALWAYS);
            if (!always)
            {
                state.ExpectKeywordsAll(Keyword.BY, Keyword.DEFAULT);
            }
            state.ExpectKeyword(Keyword.AS);
            if (state.PeekKeyword(Keyword.IDENTITY))
            {
                if (!_p.SupportsPostgresIdentitySyntax)
                {
                    throw state.ExpectedException("generated expression");
                }
                state.Next();
                return new StatementColumnOption.Generated.PgIdentity(always, ParsePostgresIdentityOptions(state));
            }
            if (!always)
            {
                throw state.ExpectedException("IDENTITY");
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
        if (_p.SupportsPostgresColumnCollationSyntax && state.ParseKeyword(Keyword.COLLATE))
        {
            if (constraintName != null)
            {
                throw new ParseException.ColumnComponentInvalidName("COLLATE", constraintName, constraintLocation);
            }
            return new StatementColumnOption.PgCollation(_objname(state));
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
        if (_p.SupportsDefaultExpressionsWithoutParentheses)
        {
            Expression expression = state.Context.HasFlag(ParseContext.DefaultExpression)
                ? _p.ExpressionParser.ParseDefaultExpr(state)
                : _p.ExpressionParser.ParseExpr(state);
            return expression is LiteralValue literal
                ? new StatementColumnOption.Default.DefaultValue(literal.Value, constraintName)
                : new StatementColumnOption.Default.DefaultExpression(expression is Nested nested ? nested.Expression : expression, constraintName);
        }
        if (state.Peek() is NumericLiteral or StringLiteral or EscapeStringLiteral or UnicodeStringLiteral or BitStringLiteral or DollarQuotedStringLiteral or NationalStringLiteral or HexStringLiteral
                or Word { Keyword: Keyword.TRUE or Keyword.FALSE or Keyword.NULL }
            && state.TryParse(ValueParser.ParseValue, out Value? val))
        {
            return new StatementColumnOption.Default.DefaultValue(val, constraintName);
        }

        return state.PeekIs<ParenOpen>()
            ? new StatementColumnOption.Default.DefaultExpression(state.ParseParenthesized(_p.ExpressionParser.ParseExpr), constraintName)
            : new StatementColumnOption.Default.DefaultExpression(_p.ExpressionParser.ParseDefaultExpr(state), constraintName);
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

    private SqlValueList<PgStatementSequenceOption>? ParsePostgresIdentityOptions(ParserState state)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return null;
        }
        SqlValueList<PgStatementSequenceOption> options = [];
        var used = new HashSet<string>(StringComparer.Ordinal);
        while (!state.PeekIs<ParenClose>())
        {
            Location location = state.Peek().Location;
            PgStatementSequenceOption option = ParseNextPostgresIdentityOption(state);
            if (!used.Add(option.UniqueName))
            {
                throw new ParseException.Duplicate(option.UniqueName, location);
            }
            options.Add(option);
        }
        if (options.Count == 0)
        {
            throw state.ExpectedException("identity sequence option");
        }
        state.ExpectRightParen();
        return options;
    }

    protected virtual PgStatementSequenceOption ParseNextPostgresIdentityOption(ParserState state)
    {
        return _p.ComponentParser.ParseSequenceOption(state) ?? throw state.ExpectedException("identity sequence option");
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
            Word { Keyword: Keyword.INDEX or Keyword.KEY } kwWord when name == null => ParseIndex(state, kwWord),
            Word { Keyword: Keyword.FULLTEXT } when name == null => ParseFullText(state),
            Word { Keyword: Keyword.SPATIAL } when name == null => ParseSpatial(state),
            _ when name != null => throw state.ExpectedCategoryException("table_constraint"),
            _ => null
        };
    }

    private StatementTableConstraint.PrimaryKey ParsePrimary(ParserState state, Identifier? name)
    {
        state.ExpectKeywordsAll(Keyword.PRIMARY, Keyword.KEY);

        IndexOrganization? indexOrganization = ParseOptionalIndexOrganization(state);
        BuiltInIndexMethod? method = null;
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
        BuiltInIndexMethod? method = null;
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
            else if (state.Peek() is Word { Keyword: Keyword.undefined })
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
            IndexMethod = method != null ? new IndexMethod.BuiltIn(method) : null,
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

    protected virtual StatementTableConstraint ParseIndex(ParserState state, Word keywordWord)
    {
        KeyLabel label = keywordWord.Keyword == Keyword.KEY ? KeyLabel.Key : KeyLabel.Index;
        state.Next();  // Consume the keyword word (KEY or INDEX)

        Identifier? name = null;
        BuiltInIndexMethod? method = null;
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
                IndexMethod = method != null ? new IndexMethod.BuiltIn(method) : null,
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
            IndexMethod = method != null ? new IndexMethod.BuiltIn(method) : null,
            Comment = comment,
            KeyLabel = label,
            IndexOrganization = indexOrganization,
            IncludedColumns = includedColumns,
            Filter = filter,
            Options = indexOptions,
            StorageLocation = storageLocation
        };
    }

    public virtual BuiltInIndexMethod ParseIndexMethod(ParserState state)
    {
        Token token = state.Next();
        return token switch
        {
            Word { Keyword: Keyword.BTREE } => BuiltInIndexMethod.Btree,
            Word { Keyword: Keyword.HASH } => BuiltInIndexMethod.Hash,
            Word { Keyword: Keyword.GIST } => BuiltInIndexMethod.Gist,
            Word { Keyword: Keyword.SPGIST } => BuiltInIndexMethod.SpGist,
            Word { Keyword: Keyword.GIN } => BuiltInIndexMethod.Gin,
            Word { Keyword: Keyword.BRIN } => BuiltInIndexMethod.Brin,
            Word { Keyword: Keyword.RTREE } => BuiltInIndexMethod.Rtree,
            _ => throw state.ExpectedException<BuiltInIndexMethod>(token)
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

        BuiltInIndexMethod? method = null;
        Comment? comment = null;
        while (true)
        {
            if (state.PeekKeyword(Keyword.COMMENT))
            {
                comment = ComponentParser.ParseComment(state);
            }
            else if (state.PeekKeyword(Keyword.USING))
            {
                state.ExpectKeyword(Keyword.USING);
                method = ParseIndexMethod(state);
            }
            else
            {
                break;
            }
        }

        return new StatementTableConstraint.Spatial(cols, name)
        {
            Comment = comment,
            KeyLabel = label,
            IndexMethod = method
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

    private static string GetIndexOptionDuplicateKey(StatementIndexOption option)
    {
        return option.GetType().Name;
    }

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

    private PgOnCommit? ParseOnCommit(ParserState state)
    {
        if (!_p.SupportsPostgresTableCreateSyntax || !state.ParseKeywordsAll(Keyword.ON, Keyword.COMMIT))
        {
            return null;
        }
        if (state.ParseKeyword(Keyword.PRESERVE))
        {
            state.ExpectKeyword(Keyword.ROWS);
            return PgOnCommit.PreserveRows;
        }
        if (state.ParseKeyword(Keyword.DELETE))
        {
            state.ExpectKeyword(Keyword.ROWS);
            return PgOnCommit.DeleteRows;
        }
        state.ExpectKeyword(Keyword.DROP);
        return PgOnCommit.Drop;
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

            if (!usedKeys.Add(curr.DuplicateCheckKey))
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

    private (bool, Select?) ParseAsSelect(ParserState state)
    {
        bool printAs = state.ParseKeyword(Keyword.AS);

        if (state.PeekKeyword(Keyword.SELECT))
        {
            Select select = _p.SelectParser.ParseSelect(state, null);

            return (printAs, select);
        }

        return (false, null);
    }

    private PgPartitionBy? ParsePartitionBy(ParserState state)
    {
        if (!_p.SupportsPostgresTableCreateSyntax || !state.ParseKeywordsAll(Keyword.PARTITION, Keyword.BY))
        {
            return null;
        }
        PgPartitionStrategy strategy = state.ParseStringEnum<PgPartitionStrategy>();
        SqlValueList<PgPartitionKey> keys = state.ParseParenthesizedCommaSeparated(ParsePartitionKey, false);
        return new PgPartitionBy(strategy, keys);
    }

    private PgPartitionKey ParsePartitionKey(ParserState state)
    {
        Expression expression = _p.ExpressionParser.ParseExpr(state);
        Expression key = expression is Collate collate ? collate.Expression : expression;
        if (key is not (SingleIdentifier or Nested or FunctionCall or Cast or Position))
        {
            throw state.ExpectedException("partition column or parenthesized expression");
        }
        if (key is FunctionCall { Over: not null } or FunctionCall { Filter: not null }
            or FunctionCall { WithinGroup: not null } or Cast { UsePostgresCastSyntax: true })
        {
            throw state.ExpectedException("partition column or parenthesized expression");
        }
        if (state.Peek() is Word { Keyword: Keyword.ASC or Keyword.DESC or Keyword.NULLS })
        {
            throw state.ExpectedException("partition key without ordering");
        }
        ObjectName? operatorClass = state.PeekIs<Word>() ? _objname(state) : null;
        return new PgPartitionKey(expression, operatorClass);
    }

    private PgPartitionBound ParsePartitionBound(ParserState state)
    {
        if (state.ParseKeyword(Keyword.DEFAULT))
        {
            return new PgPartitionBound.DefaultPartition();
        }

        state.ExpectKeywordsAll(Keyword.FOR, Keyword.VALUES);
        if (state.ParseKeyword(Keyword.IN))
        {
            return new PgPartitionBound.ListValues(state.ParseParenthesizedCommaSeparated(_p.ExpressionParser.ParseExpr, false));
        }
        if (state.ParseKeyword(Keyword.FROM))
        {
            SqlValueList<PgPartitionBound.RangeValue> from = state.ParseParenthesizedCommaSeparated(ParsePartitionRangeValue, false);
            state.ExpectKeyword(Keyword.TO);
            SqlValueList<PgPartitionBound.RangeValue> to = state.ParseParenthesizedCommaSeparated(ParsePartitionRangeValue, false);
            return new PgPartitionBound.RangeValues(from, to);
        }

        state.ExpectKeyword(Keyword.WITH);
        state.ExpectToken<ParenOpen>();

        bool remainderFirst = state.PeekKeyword(Keyword.REMAINDER);
        state.ExpectKeyword(remainderFirst ? Keyword.REMAINDER : Keyword.MODULUS);
        int first = ValueParser.ParseLiteralInt(state);

        state.ExpectToken<Comma>();

        state.ExpectKeyword(remainderFirst ? Keyword.MODULUS : Keyword.REMAINDER);
        int second = ValueParser.ParseLiteralInt(state);

        state.ExpectToken<ParenClose>();

        return new PgPartitionBound.HashValues(remainderFirst ? second : first, remainderFirst ? first : second)
        {
            RemainderFirst = remainderFirst
        };
    }

    private PgPartitionBound.RangeValue ParsePartitionRangeValue(ParserState state)
    {
        if (state.ParseKeyword(Keyword.MINVALUE))
        {
            return new PgPartitionBound.RangeValue.MinValue();
        }
        if (state.ParseKeyword(Keyword.MAXVALUE))
        {
            return new PgPartitionBound.RangeValue.MaxValue();
        }
        return new PgPartitionBound.RangeValue.ExpressionValue(_p.ExpressionParser.ParseExpr(state));
    }

    public AlterTable ParseAlter(ParserState state)
    {
        state.ExpectKeyword(Keyword.TABLE);

        bool ifExists = _p.SupportsPostgresColumnAlterSyntax && state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS);
        bool only = _p.SupportsPostgresColumnAlterSyntax && state.ParseKeyword(Keyword.ONLY);
        ObjectName tableName = _objname(state);

        var operations = new SqlValueList<AlterTableOperation>();

        while (true)
        {
            AlterTableOperation operation = ParseNextAlterOperation(state);
            if (only && !SupportsOnlyAlterOperation(operation))
            {
                throw state.ExpectedException("ALTER TABLE operation without ONLY");
            }
            operations.Add(operation);
            if (IsStandaloneAlterOperation(operation))
            {
                if (operations.Count != 1 || state.PeekIs<Comma>())
                {
                    throw state.ExpectedException("standalone ALTER TABLE operation");
                }
            }

            if (state.PeekIs<Semicolon>() || state.PeekIs<Eof>())
            {
                break;
            }

            if (!state.ConsumeTokenIs<Comma>())
            {
                throw state.ExpectedException(",");
            }
        }

        return new AlterTable(tableName, operations) { IfExists = ifExists, Only = only };
    }

    protected virtual bool IsStandaloneAlterOperation(AlterTableOperation operation)
    {
        return false;
    }

    protected virtual bool SupportsOnlyAlterOperation(AlterTableOperation operation) => true;

    protected virtual bool SupportsAlterOperation(Keyword keyword) => true;

    private AlterTableOperation ParseNextAlterOperation(ParserState state)
    {
        Word next = state.Peek() as Word ?? throw ParserState.ExpectedCategoryException("alter_table_operation", state.Peek());
        Keyword nextKeyword = next.Keyword;
        if (!SupportsAlterOperation(nextKeyword))
        {
            throw ParserState.ExpectedCategoryException("alter_table_operation", next);
        }
        if (_p.SupportsPostgresTableAlterSyntax)
        {
            if (state.ParseKeyword(Keyword.ATTACH))
            {
                state.ExpectKeyword(Keyword.PARTITION);
                ObjectName partition = _objname(state);
                return new AlterTableOperation.PgAttachPartition(partition, ParsePartitionBound(state));
            }
            if (state.ParseKeyword(Keyword.DETACH))
            {
                state.ExpectKeyword(Keyword.PARTITION);
                ObjectName partition = _objname(state);
                PgDetachPartitionMode? mode = state.MaybeParseStringEnum<PgDetachPartitionMode>();
                return new AlterTableOperation.PgDetachPartition(partition, mode);
            }
            if (nextKeyword is Keyword.ENABLE or Keyword.DISABLE)
            {
                state.Next();
                bool enable = nextKeyword == Keyword.ENABLE;
                if (state.ParseKeywordsAll(Keyword.ROW, Keyword.LEVEL, Keyword.SECURITY))
                {
                    return new AlterTableOperation.PgRowSecurity(enable ? PgRowSecurityMode.Enable : PgRowSecurityMode.Disable);
                }
                PgTriggerStateMode mode = enable ? PgTriggerStateMode.Enable : PgTriggerStateMode.Disable;
                if (enable && state.ParseKeyword(Keyword.ALWAYS))
                {
                    mode = PgTriggerStateMode.Always;
                }
                else if (enable && state.ParseKeyword(Keyword.REPLICA))
                {
                    mode = PgTriggerStateMode.Replica;
                }
                state.ExpectKeyword(Keyword.TRIGGER);
                PgAlterTriggerTarget target;
                if (mode.Kind is PgTriggerStateMode.ValueKind.Enable or PgTriggerStateMode.ValueKind.Disable
                    && state.ParseKeyword(Keyword.ALL))
                {
                    target = new PgAlterTriggerTarget.All();
                }
                else if (mode.Kind is PgTriggerStateMode.ValueKind.Enable or PgTriggerStateMode.ValueKind.Disable
                    && state.ParseKeyword(Keyword.USER))
                {
                    target = new PgAlterTriggerTarget.User();
                }
                else
                {
                    if (state.Peek() is Word { Keyword: Keyword.ALL or Keyword.USER })
                    {
                        throw state.ExpectedException("trigger name");
                    }
                    target = new PgAlterTriggerTarget.Named(_ident(state));
                }
                return new AlterTableOperation.PgTriggerState(mode, target);
            }
            if (state.ParseKeyword(Keyword.FORCE))
            {
                state.ExpectKeyword(Keyword.ROW);
                state.ExpectKeyword(Keyword.LEVEL);
                state.ExpectKeyword(Keyword.SECURITY);
                return new AlterTableOperation.PgRowSecurity(PgRowSecurityMode.Force);
            }
            if (state.ParseKeywordsAll(Keyword.NO, Keyword.FORCE))
            {
                state.ExpectKeyword(Keyword.ROW);
                state.ExpectKeyword(Keyword.LEVEL);
                state.ExpectKeyword(Keyword.SECURITY);
                return new AlterTableOperation.PgRowSecurity(PgRowSecurityMode.NoForce);
            }
            if (state.ParseKeywordsAll(Keyword.REPLICA, Keyword.IDENTITY))
            {
                if (state.ParseKeyword(Keyword.USING))
                {
                    state.ExpectKeyword(Keyword.INDEX);
                    return new AlterTableOperation.PgReplicaIdentityUsingIndex(_ident(state));
                }
                return new AlterTableOperation.PgReplicaIdentity(state.ParseStringEnum<PgReplicaIdentityMode>());
            }
            if (state.ParseKeywordsAll(Keyword.SET, Keyword.LOGGED))
            {
                return new AlterTableOperation.PgPersistence(true);
            }
            if (state.ParseKeywordsAll(Keyword.SET, Keyword.UNLOGGED))
            {
                return new AlterTableOperation.PgPersistence(false);
            }
            if (state.ParseKeywordsAll(Keyword.NO, Keyword.INHERIT))
            {
                return new AlterTableOperation.PgInherit(_objname(state)) { No = true };
            }
            if (state.ParseKeyword(Keyword.INHERIT))
            {
                return new AlterTableOperation.PgInherit(_objname(state));
            }
            if (state.ParseKeywordsAll(Keyword.NOT, Keyword.OF))
            {
                return new AlterTableOperation.PgNotOf();
            }
            if (state.ParseKeyword(Keyword.OF))
            {
                return new AlterTableOperation.PgOf(_objname(state));
            }
            PgAlterRelationOperation? relation = _p.DdlParser.ParseOptionalAlterRelationOperation(state);
            if (relation != null)
            {
                return new AlterTableOperation.PgRelation(relation);
            }
        }
        state.Next();

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
            bool printColumn = state.ParseKeyword(Keyword.COLUMN);

            StatementColumn column = ParseColumn(state);
            return new AlterTableOperation.ModifyColumn(column)
            {
                PrintColumn = printColumn,
                Position = ParseAlterTableColumnPosition(state)
            };
        }
        if (nextKeyword == Keyword.CHANGE)
        {
            bool printColumn = state.ParseKeyword(Keyword.COLUMN);

            Identifier oldName = _ident(state);
            StatementColumn newColumn = ParseColumn(state);

            return new AlterTableOperation.ChangeColumn(oldName, newColumn)
            {
                PrintColumn = printColumn,
                Position = ParseAlterTableColumnPosition(state)
            };
        }
        if (nextKeyword == Keyword.ALTER)
        {
            // CHECK ENFORCED | NOT ENFORCED
            // INDEX VISIBLE | INVISIBLE
            // COLUMN VISIBLE | INVISIBLE
            bool useColumnKeyword = state.ParseKeyword(Keyword.COLUMN);
            Identifier columnName = _ident(state);
            if (state.ParseKeywordsAll(Keyword.SET, Keyword.DEFAULT))
            {
                StatementColumnOption.Default newDefault = ParseDefault(state, null);
                return new AlterTableOperation.SetDefault(columnName, newDefault)
                {
                    PrintColumn = useColumnKeyword
                };
            }
            if (state.ParseKeywordsAll(Keyword.DROP, Keyword.DEFAULT))
            {
                return new AlterTableOperation.DropDefault(columnName)
                {
                    PrintColumn = useColumnKeyword
                };
            }
            if (_p.SupportsPostgresColumnAlterSyntax)
            {
                PgAlterColumnOperation? operation = ParseOptionalPostgresColumnAlterOperation(state);
                if (operation != null)
                {
                    return new AlterTableOperation.PgColumn(columnName, operation) { PrintColumn = useColumnKeyword };
                }
            }
            AlterTableOperation.PgIdentity? identity = ParseOptionalAlterIdentity(state, columnName, useColumnKeyword);
            if (identity != null)
            {
                return identity;
            }
            throw state.ExpectedException("SET DEFAULT", "DROP DEFAULT");
        }
        if (nextKeyword == Keyword.DROP)
        {
            return SubParseDrop(state);
        }
        if (nextKeyword == Keyword.RENAME)
        {
            return ParseAlterRename(state);
        }

        bool includeDefault = nextKeyword == Keyword.DEFAULT;
        if (includeDefault)
        {
            AlterTableOperation defaultCharsetOrCollation = SubParseAlterCharSetOrCollation(state, includeDefault)
                ?? throw state.ExpectedException("CHARACTER SET", "COLLATE");
            return defaultCharsetOrCollation;
        }
        state.Rewind();
        AlterTableOperation? charsetOrCollation = SubParseAlterCharSetOrCollation(state, false);
        if (charsetOrCollation != null)
        {
            return charsetOrCollation;
        }

        throw ParserState.ExpectedCategoryException("alter_table_operation", next);
    }

    protected virtual bool RequiresAlterTableRenameTo => false;

    protected virtual AlterTableOperation ParseAlterRename(ParserState state)
    {
        bool printColumn = state.ParseKeyword(Keyword.COLUMN);
        if (printColumn || (_p.SupportsPostgresColumnAlterSyntax && state.PeekNth(1) is Word { Keyword: Keyword.TO }))
        {
            Identifier oldColumnName = _ident(state);
            state.ExpectKeyword(Keyword.TO);
            Identifier newColumnName = _ident(state);
            return new AlterTableOperation.RenameColumn(oldColumnName, newColumnName) { PrintColumn = printColumn };
        }
        if (RequiresAlterTableRenameTo)
        {
            state.ExpectKeyword(Keyword.TO);
            return new AlterTableOperation.Rename(new ObjectName(_ident(state)));
        }
        _ = state.ParseKeywordsAny(Keyword.TO, Keyword.AS);
        return new AlterTableOperation.Rename(_objname(state));
    }

    private PgAlterColumnOperation? ParseOptionalPostgresColumnAlterOperation(ParserState state)
    {
        bool useSetData = state.ParseKeywordsAll(Keyword.SET, Keyword.DATA);
        if (useSetData || state.ParseKeyword(Keyword.TYPE))
        {
            if (useSetData)
            {
                state.ExpectKeyword(Keyword.TYPE);
            }
            DataTypeReference dataType = _p.DataTypeParser.ParseDataTypeReference(state);
            ObjectName? collation = state.ParseKeyword(Keyword.COLLATE) ? _objname(state) : null;
            Expression? usingExpression = state.ParseKeyword(Keyword.USING) ? _p.ExpressionParser.ParseExpr(state) : null;
            return new PgAlterColumnOperation.ChangeType(dataType)
            {
                UseSetData = useSetData,
                Collation = collation,
                Using = usingExpression
            };
        }
        if (state.ParseKeywordsAll(Keyword.SET, Keyword.NOT, Keyword.NULL))
        {
            return new PgAlterColumnOperation.Nullability(true);
        }
        if (state.ParseKeywordsAll(Keyword.DROP, Keyword.NOT, Keyword.NULL))
        {
            return new PgAlterColumnOperation.Nullability(false);
        }
        if (state.ParseKeywordsAll(Keyword.SET, Keyword.EXPRESSION))
        {
            state.ExpectKeyword(Keyword.AS);
            return new PgAlterColumnOperation.SetExpression(state.ParseParenthesized(_p.ExpressionParser.ParseExpr));
        }
        if (state.ParseKeywordsAll(Keyword.DROP, Keyword.EXPRESSION))
        {
            return new PgAlterColumnOperation.DropExpression(state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS));
        }
        if (state.PeekKeyword(Keyword.RESET)
            || (state.PeekKeyword(Keyword.SET) && state.PeekNth(1) is ParenOpen or Word { Keyword: Keyword.STATISTICS or Keyword.STORAGE or Keyword.COMPRESSION }))
        {
            return new PgAlterColumnOperation.Configuration(_p.DdlParser.ParseColumnConfigurationOperation(state));
        }
        return null;
    }

    private AlterTableOperation.PgIdentity? ParseOptionalAlterIdentity(ParserState state, Identifier column, bool useColumnKeyword)
    {
        if (!_p.SupportsPostgresIdentitySyntax)
        {
            return null;
        }
        SqlValueList<PgAlterIdentityOperation> operations = [];
        if (state.ParseKeyword(Keyword.ADD))
        {
            if (ParseNextColumnOption(state) is not StatementColumnOption.Generated.PgIdentity identity)
            {
                throw state.ExpectedCategoryException("GENERATED AS IDENTITY clause");
            }
            operations.Add(new PgAlterIdentityOperation.Add(identity));
        }
        else if (state.ParseKeywordsAll(Keyword.DROP, Keyword.IDENTITY))
        {
            operations.Add(new PgAlterIdentityOperation.Drop(state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS)));
        }
        else
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            while (state.Peek() is Word { Keyword: Keyword.SET or Keyword.RESTART })
            {
                Location location = state.Peek().Location;
                bool set = state.ParseKeyword(Keyword.SET);
                string key;
                PgAlterIdentityOperation operation;
                if (set && state.ParseKeyword(Keyword.GENERATED))
                {
                    bool always = state.ParseKeyword(Keyword.ALWAYS);
                    if (!always)
                    {
                        state.ExpectKeywordsAll(Keyword.BY, Keyword.DEFAULT);
                    }
                    operation = new PgAlterIdentityOperation.SetGenerated(always);
                    key = "GENERATED";
                }
                else
                {
                    PgStatementSequenceOption option = _p.ComponentParser.ParseSequenceOption(state, allowRestart: !set)
                        ?? throw state.ExpectedException("identity sequence option");
                    operation = new PgAlterIdentityOperation.SequenceOption(option);
                    key = option.UniqueName;
                }
                if (!used.Add(key))
                {
                    throw new ParseException.Duplicate(key, location);
                }
                operations.Add(operation);
            }
            if (operations.Count == 0)
            {
                return null;
            }
        }
        return new AlterTableOperation.PgIdentity(column, operations) { UseColumnKeyword = useColumnKeyword };
    }

    private AlterTableOperation? SubParseAlterCharSetOrCollation(ParserState state, bool includeDefault)
    {
        if (state.ParseKeywordsAll(Keyword.CHARACTER, Keyword.SET))
        {
            bool includeEquals = state.ConsumeTokenIs<Equal>();
            return new AlterTableOperation.SetDefaultCharacterSet(_parseString(state))
            {
                IncludeDefault = includeDefault,
                IncludeEquals = includeEquals
            };
        }
        if (state.ParseKeyword(Keyword.COLLATE))
        {
            bool includeEquals = state.ConsumeTokenIs<Equal>();
            return new AlterTableOperation.SetDefaultCollation(_parseString(state))
            {
                IncludeDefault = includeDefault,
                IncludeEquals = includeEquals
            };
        }
        return null;
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
        bool printKeyword = state.ParseKeyword(Keyword.COLUMN);
        bool ifNotExists = _p.SupportsPostgresColumnAlterSyntax && state.ParseKeywordsAll(Keyword.IF, Keyword.NOT, Keyword.EXISTS);

        StatementColumn column = ParseColumn(state);
        return new AlterTableOperation.AddColumn(column, printKeyword)
        {
            Position = ParseAlterTableColumnPosition(state),
            IfNotExists = ifNotExists
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

        bool printColumn = state.ParseKeyword(Keyword.COLUMN);
        bool ifExists = _p.SupportsPostgresColumnAlterSyntax && state.ParseKeywordsAll(Keyword.IF, Keyword.EXISTS);
        Identifier columnName = _ident(state);
        return new AlterTableOperation.DropColumn(columnName)
        {
            PrintColumn = printColumn,
            IfExists = ifExists,
            PgBehavior = _p.SupportsPostgresColumnAlterSyntax ? state.MaybeParseStringEnum<PgDropBehavior>() : null
        };
    }


    protected virtual AlterTableColumnPosition? ParseAlterTableColumnPosition(ParserState state)
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
