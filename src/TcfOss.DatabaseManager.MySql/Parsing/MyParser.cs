using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.BuiltIn;

using SetDelimiter = TcfOss.DatabaseManager.MySql.Lexing.Tokens.SetDelimiter;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyParser : Parser
{
    public MyParser()
    {
        PrecedenceManager = new PrecedenceManager();
        ValueParser = new ValueParser();
        IsBuiltInFunction = new MyFunctionNameProvider().IsBuiltInFunction;
        DataTypeParser = new MyDataTypeParser();
        ComponentParser = new ComponentParser(this);
        ExpressionParser = new MyExpressionParser(this);
        SelectParser = new MySelectParser(this);
        DmlParser = new MyDmlParser(this);
        DdlParser = new MyDdlParser(this);
        TableParser = new MyTableParser(this);
        ControlFlowParser = new MyControlFlowParser(this);
    }

    public override SqlValueList<Statement> Parse(ParserState state, EndSubstatements? endChecker)
    {
        SqlValueList<Statement> statements = [];
        bool expectingTerminator = false;
        string? terminator = null;

        while (true)
        {
            if (terminator != null)
            {
                string currentTerminator = terminator;
                while (state.ConsumeTokenIsIf<MyTerminator>((x) => x.Delimiter == currentTerminator))
                {
                    expectingTerminator = false;
                }
            }
            else
            {
                while (state.ConsumeTokenIs<Semicolon>())
                {
                    expectingTerminator = false;
                }
            }

            Token next = state.Peek();
            if (next is Eof)
            {
                if (endChecker != null)
                {
                    throw endChecker.GetEofException(state);
                }
                if (next.PreNonSql.SafeAny())
                {
                    statements.Add(next.PreNonSql.ToInertOnly());
                }
                break;
            }

            if (endChecker?.GetFinished(state) ?? false)
            {
                if (next.PreNonSql.SafeAny())
                {
                    statements.Add(next.PreNonSql.ToInertOnly());
                }
                break;
            }

            if (expectingTerminator)
            {
                throw ParserState.ExpectedException("end of statement", next);
            }

            Statement statement = ParseStatement(state);
            if (statement is Statements.SetDelimiter sd)
            {
                terminator = sd.Delimiter == ";" ? null : sd.Delimiter;
            }
            else
            {
                statements.Add(statement);
                expectingTerminator = true;
            }
        }

        return statements;
    }

    protected override Statement ParseNormalStatement(ParserState state, Token token, Label? label)
    {
        return token switch
        {
            SetDelimiter => ParseSetDelimiter(state),
            Word word => ParseKeywordStatement(state, word, label),
            ParenOpen => SelectParser.ParseSelect(state, null),
            _ => throw ParserState.ExpectedException("SQL statement".Italic(), token)
        };
    }

    protected override Statement ParseKeywordStatement(ParserState state, Word first, Label? label)
    {
        Statement? statement = first.Keyword switch
        {
            Keyword.LOOP => ParseLoop(state, label),
            Keyword.SIGNAL => ParseSignal(state),
            Keyword.RESIGNAL => ParseResignal(state),
            Keyword.START => ParseStartTransaction(state),
            Keyword.ROLLBACK => ParseRollback(state),
            Keyword.COMMIT => ParseCommit(state),
            Keyword.PREPARE => ParsePrepare(state),
            Keyword.EXECUTE => ParseExecute(state),
            Keyword.DROP => ParseDrop(state),
            Keyword.DEALLOCATE => ParseDeallocate(state),
            Keyword.SHOW => ParseShow(state),
            Keyword.CALL => ParseCall(state),
            _ => null
        };

        if (statement != null)
        {
            return statement;
        }

        return base.ParseKeywordStatement(state, first, label);
    }

    private static Statements.SetDelimiter ParseSetDelimiter(ParserState state)
    {
        if (state.Peek() is not SetDelimiter next)
        {
            throw state.ExpectedException("DELIMITER");
        }
        state.Next();
        return new Statements.SetDelimiter(next.Delimiter);
    }

    protected override Fetch ParseFetch(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.FETCH, Keyword.GROUP, Keyword.NEXT, Keyword.ROW))
        {
            return new FetchGroupNextRow();
        }
        return base.ParseFetch(state);
    }

    private Loop ParseLoop(ParserState state, Label? label)
    {
        state.ExpectKeyword(Keyword.LOOP);

        var ender = new EndSubstatements
        {
            GetFinished = s => s.ParseKeywordsAll(Keyword.END, Keyword.LOOP),
            GetEofException = s => s.ExpectedException("END LOOP")
        };

        SqlValueList<Statement> subStatements = Parse(state, ender);

        Identifier? endLabel = null;
        if (label != null && state.Peek() is Word w && w.Value == label.Identifier.Name)
        {
            state.Next();
            endLabel = label.Identifier;
        }

        return new Loop(subStatements) { EndLabel = endLabel };
    }

    private SignalInformationItem ParseSignalInformationItem(ParserState state)
    {
        Token next = state.Next();
        if (next is not Word w)
        {
            throw ParserState.ExpectedCategoryException("signal_property_name", next);
        }

        SignalPropertyName propertyName = w.Keyword switch
        {
            Keyword.CLASS_ORIGIN => SignalPropertyName.ClassOrigin,
            Keyword.SUBCLASS_ORIGIN => SignalPropertyName.SubclassOrigin,
            Keyword.MESSAGE_TEXT => SignalPropertyName.MessageText,
            Keyword.MYSQL_ERRNO => SignalPropertyName.MySqlErrno,
            Keyword.CONSTRAINT_CATALOG => SignalPropertyName.ConstraintCatalog,
            Keyword.CONSTRAINT_SCHEMA => SignalPropertyName.ConstraintSchema,
            Keyword.CONSTRAINT_NAME => SignalPropertyName.ConstraintName,
            Keyword.CATALOG_NAME => SignalPropertyName.CatalogName,
            Keyword.SCHEMA_NAME => SignalPropertyName.SchemaName,
            Keyword.TABLE_NAME => SignalPropertyName.TableName,
            Keyword.COLUMN_NAME => SignalPropertyName.ColumnName,
            Keyword.CURSOR_NAME => SignalPropertyName.CursorName,
            _ => throw ParserState.ExpectedCategoryException("signal_property_name", w)
        };

        state.ExpectToken<Equal>();

        Expression value = ExpressionParser.ParseExpr(state);

        return new SignalInformationItem(propertyName, value);
    }

    private Signal ParseSignal(ParserState state)
    {
        state.ExpectKeyword(Keyword.SIGNAL);

        SignalConditionValue conditionValue;
        if (state.ParseKeyword(Keyword.SQLSTATE))
        {
            _ = state.ParseKeyword(Keyword.VALUE);
            if (state.Next() is not StringLiteral value)
            {
                throw ParserState.ExpectedCategoryException("sqlstate_value", state.PeekNth(-1));
            }
            conditionValue = new SignalConditionValue.SqlState(value.Value);
        }
        else
        {
            if (state.Next() is not Word word)
            {
                throw ParserState.ExpectedCategoryException("condition_name", state.PeekNth(-1));
            }
            conditionValue = new SignalConditionValue.ConditionName(word.Value);
        }

        SqlValueList<SignalInformationItem> informationItems = state.ParseKeyword(Keyword.SET)
            ? state.ParseCommaSeparated(ParseSignalInformationItem)
            : [];


        return new Signal(conditionValue, informationItems);
    }

    private Resignal ParseResignal(ParserState state)
    {
        state.ExpectKeyword(Keyword.RESIGNAL);

        SignalConditionValue? conditionValue = null;
        if (state.ParseKeyword(Keyword.SQLSTATE))
        {
            bool includeValueKeyword = state.ParseKeyword(Keyword.VALUE);
            if (state.Next() is not StringLiteral value)
            {
                state.Rewind();
                throw state.ExpectedCategoryException("sqlstate_value");
            }
            conditionValue = new SignalConditionValue.SqlState(value.Value)
            {
                IncludeValueKeyword = includeValueKeyword
            };
        }
        else if (state.Peek() is Word w)
        {
            conditionValue = new SignalConditionValue.ConditionName(w.Value);
            state.Next();
        }

        SqlValueList<SignalInformationItem> informationItems = state.ParseKeyword(Keyword.SET)
            ? state.ParseCommaSeparated(ParseSignalInformationItem)
            : [];

        return new Resignal
        {
            ConditionValue = conditionValue,
            InformationItems = informationItems
        };
    }

    private static StartTransaction ParseStartTransaction(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.START, Keyword.TRANSACTION);

        SqlValueList<MySqlTransactionCharacteristic> characteristics = [];
        while (true)
        {
            if (state.ParseKeywordsAll(Keyword.WITH, Keyword.CONSISTENT, Keyword.SNAPSHOT))
            {
                characteristics.Add(MySqlTransactionCharacteristic.WithConsistentSnapshot);
            }
            else if (state.ParseKeyword(Keyword.READ))
            {
                if (state.ParseKeyword(Keyword.ONLY))
                {
                    characteristics.Add(MySqlTransactionCharacteristic.ReadOnly);
                }
                else if (state.ParseKeyword(Keyword.WRITE))
                {
                    characteristics.Add(MySqlTransactionCharacteristic.ReadWrite);
                }
                else
                {
                    throw state.ExpectedException("ONLY", "WRITE");
                }
            }
            else
            {
                break;
            }
        }

        return new StartTransaction
        {
            Characteristics = characteristics
        };
    }

    private static (TransactionNoun? transactionNoun, TransactionChainOption? chainOption, TransactionReleaseOption? releaseOption) ParseTransactionOptions(ParserState state)
    {
        TransactionNoun? noun = null;
        if (state.ParseKeyword(Keyword.WORK))
        {
            noun = TransactionNoun.Work;
        }

        TransactionChainOption? chainOption = null;
        TransactionReleaseOption? releaseOption = null;
        while (true)
        {
            if (state.ParseKeyword(Keyword.AND))
            {
                bool negated = state.ParseKeyword(Keyword.NO);
                state.ExpectKeyword(Keyword.CHAIN);
                chainOption = new TransactionChainOption { Negated = negated };
            }
            else if (state.ParseKeyword(Keyword.NO))
            {
                state.ExpectKeyword(Keyword.RELEASE);
                releaseOption = new TransactionReleaseOption { Negated = true };
            }
            else if (state.ParseKeyword(Keyword.RELEASE))
            {
                releaseOption = new TransactionReleaseOption { Negated = false };
            }
            else
            {
                break;
            }
        }

        return (noun, chainOption, releaseOption);
    }

    private static Rollback ParseRollback(ParserState state)
    {
        state.ExpectKeyword(Keyword.ROLLBACK);

        (
            TransactionNoun? noun,
            TransactionChainOption? chainOption,
            TransactionReleaseOption? releaseOption) = ParseTransactionOptions(state);

        return new Rollback
        {
            Noun = noun,
            ChainOption = chainOption,
            ReleaseOption = releaseOption
        };
    }

    private static Commit ParseCommit(ParserState state)
    {
        state.ExpectKeyword(Keyword.COMMIT);

        (
            TransactionNoun? noun,
            TransactionChainOption? chainOption,
            TransactionReleaseOption? releaseOption) = ParseTransactionOptions(state);

        return new Commit
        {
            Noun = noun,
            ChainOption = chainOption,
            ReleaseOption = releaseOption
        };
    }

    private Prepare ParsePrepare(ParserState state)
    {
        state.ExpectKeyword(Keyword.PREPARE);

        Identifier statementName = ComponentParser.ParseIdentifier(state);

        state.ExpectKeyword(Keyword.FROM);

        if (state.PeekIs<StringLiteral>())
        {
            return new Prepare.FromString(statementName, new Value.SingleQuotedString(((StringLiteral)state.Next()).Value));
        }

        Identifier statementSource = ComponentParser.ParseIdentifier(state);

        return new Prepare.FromVariable(statementName, statementSource);
    }

    private MyExecute ParseExecute(ParserState state)
    {
        state.ExpectKeyword(Keyword.EXECUTE);
        Identifier statementName = ComponentParser.ParseIdentifier(state);

        SqlValueList<Identifier>? variables = null;
        if (state.ParseKeyword(Keyword.USING))
        {
            variables = state.ParseCommaSeparated(ComponentParser.ParseIdentifier);
        }

        return new MyExecute(statementName)
        {
            Variables = variables
        };
    }

    private Statement ParseDrop(ParserState state)
    {
        state.ExpectKeyword(Keyword.DROP);
        if (state.ParseKeyword(Keyword.PREPARE))
        {
            Identifier toDrop = ComponentParser.ParseIdentifier(state);
            return new DeallocatePrepare(toDrop, DeallocatePrepareLabel.Drop);
        }
        state.Rewind();
        return DdlParser.ParseDropObject(state);
    }

    private DeallocatePrepare ParseDeallocate(ParserState state)
    {
        state.ExpectKeyword(Keyword.DEALLOCATE);
        state.ExpectKeyword(Keyword.PREPARE);

        Identifier toDrop = ComponentParser.ParseIdentifier(state);
        return new DeallocatePrepare(toDrop, DeallocatePrepareLabel.Deallocate);
    }

    private Statement ParseShow(ParserState state)
    {
        state.ExpectKeyword(Keyword.SHOW);

        return state.Peek() switch
        {
            Word { Keyword: Keyword.ERRORS } => ParseShowDiagnostic(state),
            Word { Keyword: Keyword.WARNINGS } => ParseShowDiagnostic(state),
            Word { Keyword: Keyword.COUNT } => ParseShowDiagnostic(state),
            _ => throw state.ExpectedException("ERRORS", "WARNINGS", "COUNT")
        };
    }

    private Statement ParseShowDiagnostic(ParserState state)
    {
        if (state.ParseKeyword(Keyword.COUNT))
        {
            state.ExpectToken<ParenOpen>();
            state.ExpectToken<Asterisk>();
            state.ExpectToken<ParenClose>();

            if (state.ParseKeyword(Keyword.ERRORS))
            {
                return new ShowDiagnostic.Count(ShowDiagnosticType.Errors);
            }
            if (state.ParseKeyword(Keyword.WARNINGS))
            {
                return new ShowDiagnostic.Count(ShowDiagnosticType.Warnings);
            }
            throw state.ExpectedException("WARNINGS", "ERRORS");
        }

        ShowDiagnosticType? diagnosticType = state.Next() switch
        {
            Word { Keyword: Keyword.ERRORS } => ShowDiagnosticType.Errors,
            Word { Keyword: Keyword.WARNINGS } => ShowDiagnosticType.Warnings,
            _ => null
        };
        if (diagnosticType == null)
        {
            state.Rewind();
            throw state.ExpectedException("ERRORS", "WARNINGS");
        }

        Limit? limit = null;
        if (state.PeekKeyword(Keyword.LIMIT))
        {
            limit = ComponentParser.ParseLimit(state);
        }

        return new ShowDiagnostic.Values(diagnosticType)
        {
            Limit = limit
        };
    }

    private Call ParseCall(ParserState state)
    {
        state.ExpectKeyword(Keyword.CALL);

        Expression functionExpr = ExpressionParser.ParseExpr(state);

        return new Call(functionExpr);
    }
}
