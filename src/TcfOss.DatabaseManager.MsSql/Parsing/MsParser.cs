using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Statements;
using TcfOss.DatabaseManager.MsSql.Statements.Components;
using MsBatchSeparatorStatement = TcfOss.DatabaseManager.MsSql.Statements.MsBatchSeparator;
using MsBatchSeparatorToken = TcfOss.DatabaseManager.MsSql.Lexing.Tokens.MsBatchSeparator;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsParser : Parser
{
    public override QuoteStyle DefaultQuoteStyle => QuoteStyle.Brackets;

    public MsParser()
    {
        PrecedenceManager = new PrecedenceManager();
        ValueParser = new ValueParser();
        IsBuiltInFunction = new MsFunctionNameProvider().IsBuiltInFunction;
        DataTypeParser = new MsDataTypeParser();
        ComponentParser = new ComponentParser(this);
        ExpressionParser = new ExpressionParser(this);
        SelectParser = new MsSelectParser(this);
        DmlParser = new MsDmlParser(this);
        DdlParser = new MsDdlParser(this);
        TableParser = new TableParser(this);
        ControlFlowParser = new MsControlFlowParser(this);
    }

    public override SqlValueList<Statement> Parse(ParserState state, EndSubstatements? endChecker)
    {
        SqlValueList<Statement> statements = [];
        bool expectingTerminator = false;

        while (true)
        {
            while (state.ConsumeTokenIs<Semicolon>())
            {
                expectingTerminator = false;
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

            // A GO batch separator implicitly ends the previous statement;
            // it does not need a preceding semicolon.
            if (next is MsBatchSeparatorToken)
            {
                expectingTerminator = false;
            }

            if (expectingTerminator)
            {
                throw ParserState.ExpectedException("end of statement", next);
            }

            Statement statement = ParseStatement(state);
            statements.Add(statement);

            // GO itself does not need a terminator afterwards.
            expectingTerminator = statement is not MsBatchSeparatorStatement;
        }

        return statements;
    }

    protected override Statement ParseNormalStatement(ParserState state, Token token, Label? label)
    {
        return token switch
        {
            MsBatchSeparatorToken => ParseBatchSeparator(state),
            _ => base.ParseNormalStatement(state, token, label)
        };
    }

    protected override Statement ParseKeywordStatement(ParserState state, Word first, Label? label)
    {
        return first.Keyword switch
        {
            Keyword.EXEC or Keyword.EXECUTE => ParseExecute(state),
            Keyword.WHILE => ParseWhile(state),
            Keyword.BREAK => ParseBreak(state),
            Keyword.CONTINUE => ParseContinue(state),
            Keyword.COMMIT => ParseCommit(state),
            Keyword.ROLLBACK => ParseRollback(state),
            Keyword.SAVE => ParseSaveTransaction(state),
            Keyword.PRINT => ParsePrint(state),
            Keyword.THROW => ParseThrow(state),
            Keyword.RAISERROR => ParseRaiseError(state),
            _ => base.ParseKeywordStatement(state, first, label)
        };
    }

    private static MsBatchSeparatorStatement ParseBatchSeparator(ParserState state)
    {
        if (state.Next() is not MsBatchSeparatorToken token)
        {
            throw state.ExpectedException("GO");
        }
        return new MsBatchSeparatorStatement(token.Count);
    }

    /// <summary>
    /// Parses a T-SQL <c>EXEC[UTE]</c> statement. Supports the procedure-call
    /// form (with optional return-status capture, named or positional
    /// arguments, and <c>OUTPUT</c>/<c>DEFAULT</c> argument modifiers) and the
    /// parenthesized dynamic-SQL form <c>EXEC (string_expression)</c>.
    /// </summary>
    private MsExecute ParseExecute(ParserState state)
    {
        Keyword kw = state.ParseKeywordsAny(Keyword.EXEC, Keyword.EXECUTE);
        if (kw == Keyword.undefined)
        {
            throw state.ExpectedException("EXEC", "EXECUTE");
        }
        bool useExecute = kw == Keyword.EXECUTE;

        // Dynamic-SQL form: EXEC ( <string expression> )
        if (state.ConsumeTokenIs<ParenOpen>())
        {
            Expression dynamicSql = ExpressionParser.ParseExpr(state);
            state.ExpectRightParen();
            return new MsExecute.DynamicSqlCall(dynamicSql)
            {
                UseExecute = useExecute,
            };
        }

        // Optional return-status capture: EXEC @ret = proc_name ...
        Identifier? returnVariable = null;
        if (state.Peek() is Word { Sigil: SigilKind.Variable } && state.PeekNth(1) is Equal)
        {
            returnVariable = ComponentParser.ParseIdentifier(state);
            state.Next(); // consume '='
        }

        ObjectName procName = ComponentParser.ParseObjectName(state);

        SqlValueList<MsExecuteArgument>? arguments = null;
        if (CanBeginExecuteArgument(state.Peek()))
        {
            arguments = state.ParseCommaSeparated(ParseExecuteArgument);
        }

        return new MsExecute.ProcedureCall(procName)
        {
            UseExecute = useExecute,
            ReturnVariable = returnVariable,
            Arguments = arguments,
        };
    }

    /// <summary>
    /// Parses a single argument to an <c>EXEC[UTE]</c> procedure invocation:
    /// <c>[@name =] {expression | DEFAULT} [OUTPUT | OUT]</c>.
    /// </summary>
    private MsExecuteArgument ParseExecuteArgument(ParserState state)
    {
        Identifier? name = null;
        if (state.Peek() is Word { Sigil: SigilKind.Variable } && state.PeekNth(1) is Equal)
        {
            name = ComponentParser.ParseIdentifier(state);
            state.Next(); // consume '='
        }

        if (state.ParseKeyword(Keyword.DEFAULT))
        {
            return new MsExecuteArgument.DefaultValue
            {
                Name = name,
                IsOutput = state.ParseKeywordsAny(Keyword.OUTPUT, Keyword.OUT) != Keyword.undefined,
            };
        }

        Expression value = ExpressionParser.ParseExpr(state);
        return new MsExecuteArgument.ExpressionValue(value)
        {
            Name = name,
            IsOutput = state.ParseKeywordsAny(Keyword.OUTPUT, Keyword.OUT) != Keyword.undefined,
        };
    }

    /// <summary>
    /// Conservative check for whether the upcoming token can begin an EXEC
    /// argument. Stops on terminators and tokens that start a sibling
    /// statement so an arg-less <c>EXEC proc</c> doesn't try to consume the
    /// next statement.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Spurious counts due to IL-lowering clutter Risk Hotspots. Logic is actually trivial.")]
    private static bool CanBeginExecuteArgument(Token next)
    {
        return next switch
        {
            Semicolon or Eof or MsBatchSeparatorToken or ParenClose or Comma => false,
            Word w => w.Keyword switch
            {
                Keyword.END
                    or Keyword.ELSE
                    or Keyword.SELECT
                    or Keyword.INSERT
                    or Keyword.UPDATE
                    or Keyword.DELETE
                    or Keyword.EXEC
                    or Keyword.EXECUTE
                    or Keyword.CREATE
                    or Keyword.ALTER
                    or Keyword.DROP
                    or Keyword.TRUNCATE
                    or Keyword.DECLARE
                    or Keyword.SET
                    or Keyword.IF
                    or Keyword.WHILE
                    or Keyword.BEGIN
                    or Keyword.RETURN
                    or Keyword.USE
                    or Keyword.OPEN
                    or Keyword.CLOSE
                    or Keyword.FETCH
                    or Keyword.LEAVE
                    or Keyword.ITERATE
                    or Keyword.WITH
                    or Keyword.VALUES => false,
                _ => true,
            },
            _ => true,
        };
    }

    /// <summary>
    /// Parses a T-SQL <c>WHILE</c> loop:
    /// <c>WHILE &lt;bool-expr&gt; &lt;stmt&gt;</c>.
    /// </summary>
    private While ParseWhile(ParserState state)
    {
        state.ExpectKeyword(Keyword.WHILE);
        Expression condition = ExpressionParser.ParseExpr(state);
        Statement body = ParseStatement(state);
        return new While(condition, body);
    }

    /// <summary>
    /// Parses a T-SQL <c>BREAK</c> statement.
    /// </summary>
    private static Break ParseBreak(ParserState state)
    {
        state.ExpectKeyword(Keyword.BREAK);
        return new Break();
    }

    /// <summary>
    /// Parses a T-SQL <c>CONTINUE</c> statement.
    /// </summary>
    private static Continue ParseContinue(ParserState state)
    {
        state.ExpectKeyword(Keyword.CONTINUE);
        return new Continue();
    }

    /// <summary>
    /// Parses a T-SQL <c>BEGIN</c>. Dispatches between
    /// <c>BEGIN…END</c> blocks, <c>BEGIN TRY…END TRY BEGIN CATCH…END CATCH</c>,
    /// and <c>BEGIN { TRAN | TRANSACTION } [name]</c>.
    /// </summary>
    protected override Statement ParseBegin(ParserState state)
    {
        Token next = state.PeekNth(1);
        if (next is Word { Keyword: Keyword.TRY })
        {
            return ParseTryCatch(state);
        }
        if (next is Word { Keyword: Keyword.TRAN or Keyword.TRANSACTION })
        {
            return ParseBeginTransaction(state);
        }
        return base.ParseBegin(state);
    }

    /// <summary>
    /// Parses a T-SQL <c>BEGIN TRY … END TRY BEGIN CATCH … END CATCH</c> block.
    /// </summary>
    private MsTryCatch ParseTryCatch(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.BEGIN, Keyword.TRY);
        var tryEnder = new EndSubstatements()
        {
            GetFinished = s => s.PeekKeywordsAllEqual(Keyword.END, Keyword.TRY),
            GetEofException = s => s.ExpectedException("END TRY"),
        };
        SqlValueList<Statement> tryStatements = Parse(state, tryEnder);
        state.ExpectKeywordsAll(Keyword.END, Keyword.TRY);
        state.ExpectKeywordsAll(Keyword.BEGIN, Keyword.CATCH);
        var catchEnder = new EndSubstatements()
        {
            GetFinished = s => s.PeekKeywordsAllEqual(Keyword.END, Keyword.CATCH),
            GetEofException = s => s.ExpectedException("END CATCH"),
        };
        SqlValueList<Statement> catchStatements = Parse(state, catchEnder);
        state.ExpectKeywordsAll(Keyword.END, Keyword.CATCH);
        return new MsTryCatch(tryStatements, catchStatements);
    }

    /// <summary>
    /// Parses a T-SQL <c>BEGIN { TRAN | TRANSACTION } [transaction_name]</c>.
    /// </summary>
    private MsBeginTransaction ParseBeginTransaction(ParserState state)
    {
        state.ExpectKeyword(Keyword.BEGIN);
        TransactionNoun noun = TryParseTransactionNoun(state)
            ?? throw state.ExpectedException("TRAN", "TRANSACTION");
        Identifier? name = TryParseTransactionName(state);
        return new MsBeginTransaction(noun) { Name = name };
    }

    /// <summary>
    /// Parses a T-SQL <c>COMMIT [ { TRAN | TRANSACTION } [ transaction_name ] ]</c>.
    /// </summary>
    private MsCommit ParseCommit(ParserState state)
    {
        state.ExpectKeyword(Keyword.COMMIT);
        TransactionNoun? noun = TryParseTransactionNoun(state);
        Identifier? name = noun != null ? TryParseTransactionName(state) : null;
        return new MsCommit { Noun = noun, Name = name };
    }

    /// <summary>
    /// Parses a T-SQL <c>ROLLBACK [ { TRAN | TRANSACTION } [ transaction_or_savepoint_name ] ]</c>.
    /// </summary>
    private MsRollback ParseRollback(ParserState state)
    {
        state.ExpectKeyword(Keyword.ROLLBACK);
        TransactionNoun? noun = TryParseTransactionNoun(state);
        Identifier? name = noun != null ? TryParseTransactionName(state) : null;
        return new MsRollback { Noun = noun, Name = name };
    }

    /// <summary>
    /// Parses a T-SQL <c>SAVE { TRAN | TRANSACTION } savepoint_name</c>.
    /// </summary>
    private MsSaveTransaction ParseSaveTransaction(ParserState state)
    {
        state.ExpectKeyword(Keyword.SAVE);
        TransactionNoun noun = TryParseTransactionNoun(state)
            ?? throw state.ExpectedException("TRAN", "TRANSACTION");
        Identifier name = ComponentParser.ParseIdentifier(state);
        return new MsSaveTransaction(noun, name);
    }

    /// <summary>
    /// Consumes a <c>TRAN</c> or <c>TRANSACTION</c> keyword if present and
    /// returns the corresponding <see cref="TransactionNoun"/>, or null if
    /// neither is next. (T-SQL does not accept the SQL-standard <c>WORK</c>
    /// noun in transaction-control statements.)
    /// </summary>
    private static TransactionNoun? TryParseTransactionNoun(ParserState state)
    {
        Keyword keyword = state.ParseKeywordsAny(Keyword.TRAN, Keyword.TRANSACTION);
        return keyword == Keyword.undefined ? null : TransactionNoun.Parse(keyword.ToString());
    }

    private Identifier? TryParseTransactionName(ParserState state)
    {
        return state.Peek() is Word { Keyword: Keyword.undefined }
            ? ComponentParser.ParseIdentifier(state)
            : null;
    }

    /// <summary>
    /// Parses a T-SQL <c>RETURN</c> statement. The return expression is
    /// optional (procedures return a status code, functions may return a
    /// value). When the next token starts a new statement or terminator,
    /// <see cref="Return.Body"/> is left null.
    /// </summary>
    protected override Return ParseReturn(ParserState state)
    {
        state.ExpectKeyword(Keyword.RETURN);
        if (IsStatementTerminator(state.Peek()))
        {
            return new Return(null);
        }
        return new Return(ExpressionParser.ParseExpr(state));
    }

    /// <summary>
    /// Parses a T-SQL <c>PRINT &lt;expression&gt;</c> statement.
    /// </summary>
    private MsPrint ParsePrint(ParserState state)
    {
        state.ExpectKeyword(Keyword.PRINT);
        Expression body = ExpressionParser.ParseExpr(state);
        return new MsPrint(body);
    }

    /// <summary>
    /// Parses a T-SQL <c>THROW [ error_number , message , state ]</c> statement.
    /// The bare form has no arguments and is only valid inside a CATCH block
    /// (the parser does not enforce that).
    /// </summary>
    private MsThrow ParseThrow(ParserState state)
    {
        state.ExpectKeyword(Keyword.THROW);
        if (IsStatementTerminator(state.Peek()))
        {
            return new MsThrow(null);
        }
        Expression errorNumber = ExpressionParser.ParseExpr(state);
        state.ExpectToken<Comma>();
        Expression message = ExpressionParser.ParseExpr(state);
        state.ExpectToken<Comma>();
        Expression stateExpr = ExpressionParser.ParseExpr(state);
        return new MsThrow(new MsThrowable(errorNumber, message, stateExpr));
    }

    /// <summary>
    /// Parses a T-SQL basic <c>RAISERROR ( message , severity , state [ , argument , ... ] )</c>.
    /// </summary>
    // TODO: Support the WITH LOG | NOWAIT | SETERROR options.
    private MsRaiseError ParseRaiseError(ParserState state)
    {
        state.ExpectKeyword(Keyword.RAISERROR);
        state.ExpectLeftParen();
        Expression message = ExpressionParser.ParseExpr(state);
        state.ExpectToken<Comma>();
        Expression severity = ExpressionParser.ParseExpr(state);
        state.ExpectToken<Comma>();
        Expression stateExpr = ExpressionParser.ParseExpr(state);

        SqlValueList<Expression>? arguments = null;
        while (state.ConsumeTokenIs<Comma>())
        {
            arguments ??= [];
            arguments.Add(ExpressionParser.ParseExpr(state));
        }
        state.ExpectRightParen();

        return new MsRaiseError(message, severity, stateExpr) { Arguments = arguments };
    }

    private static bool IsStatementTerminator(Token next)
    {
        return next switch
        {
            Semicolon or Eof or MsBatchSeparatorToken or ParenClose => true,
            Word w => w.Keyword switch
            {
                Keyword.END or Keyword.ELSE
                    or Keyword.SELECT or Keyword.INSERT or Keyword.UPDATE or Keyword.DELETE
                    or Keyword.EXEC or Keyword.EXECUTE
                    or Keyword.CREATE or Keyword.ALTER or Keyword.DROP or Keyword.TRUNCATE
                    or Keyword.DECLARE or Keyword.SET or Keyword.IF or Keyword.WHILE
                    or Keyword.BEGIN or Keyword.RETURN or Keyword.USE
                    or Keyword.OPEN or Keyword.CLOSE or Keyword.FETCH
                    or Keyword.BREAK or Keyword.CONTINUE => true,
                _ => false,
            },
            _ => false,
        };
    }
}
