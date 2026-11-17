using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Statements;
using TcfOss.DatabaseManager.MsSql.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;
using MsBatchSeparatorStatement = TcfOss.DatabaseManager.MsSql.Statements.MsBatchSeparator;
using MsBatchSeparatorToken = TcfOss.DatabaseManager.MsSql.Lexing.Tokens.MsBatchSeparator;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsParser : Parser
{
    public override Grammar Grammar => Grammar.Ms;

    public override QuoteStyle DefaultQuoteStyle => QuoteStyle.Brackets;
    public override bool SupportsPostgresCastSyntax => false;
    public override bool SupportsPostgresIdentitySyntax => false;
    public override bool SupportsPostgresColumnCollationSyntax => false;
    public override bool SupportsPostgresColumnAlterSyntax => false;
    public override bool SupportsPostgresTableCreateSyntax => false;
    public override bool SupportsPostgresConstraintSyntax => false;
    public override bool SupportsPostgresTableAlterSyntax => false;
    public override bool SupportsPostgresExpressionCollationSyntax => false;
    public override bool SupportsPostgresArrayAndRowExpressionSyntax => false;
    public override bool SupportsPostgresOperatorSyntax => false;

    public MsParser()
    {
        PrecedenceManager = new MsPrecedenceManager();
        ValueParser = new ValueParser();
        IsBuiltInFunction = new MsFunctionNameProvider().IsBuiltInFunction;
        DataTypeParser = new MsDataTypeParser(this);
        ComponentParser = new MsComponentParser(this);
        ExpressionParser = new MsExpressionParser(this);
        SelectParser = new MsSelectParser(this);
        DmlParser = new MsDmlParser(this);
        DdlParser = new MsDdlParser(this);
        TableParser = new TableParser(this);
        ControlFlowParser = new MsControlFlowParser(this);
    }

    protected override CreateObjectOptions ParseCreateObjectOptions(ParserState state)
    {
        CreateOrLabel? createOrLabel = ComponentParser.ParseCreateOrLabel(state);
        return new CreateObjectOptions
        {
            CreateOrLabel = createOrLabel,
            AllSupportedTypes = ["TABLE", "TRIGGER", "VIEW", "FUNCTION", "PROCEDURE", "INDEX"]
        };
    }

    protected override Call ParseCall(ParserState state)
    {
        throw ParserState.ExpectedException("SQL statement".Italic(), state.Peek());
    }

    protected override void GuardCreateOptions(ParserState state, Word objectTypeToken, CreateObjectOptions options)
    {
        Keyword kw = objectTypeToken.Keyword;
        if (options.CreateOrLabel != null && kw is not (Keyword.TRIGGER or Keyword.FUNCTION or Keyword.PROCEDURE or Keyword.VIEW))
        {
            throw state.ExpectedException("TRIGGER", "FUNCTION", "PROCEDURE", "VIEW");
        }
    }

    protected override Statement ParseCreate(ParserState state)
    {
        state.ExpectKeyword(Keyword.CREATE);

        CreateObjectOptions options = ParseCreateObjectOptions(state);
        if (state.Peek() is not Word objectTypeWord)
        {
            throw state.ExpectedException(options.AllSupportedTypes);
        }
        GuardCreateOptions(state, objectTypeWord, options);

        return objectTypeWord.Keyword switch
        {
            Keyword.TABLE => TableParser.ParseCreate(state, options.CreateOrLabel == CreateOrLabel.OrAlter, options.TemporaryLabel),
            Keyword.TRIGGER => DdlParser.ParseCreateTrigger(state, options.CreateOrLabel, options.Definer),
            Keyword.FUNCTION => DdlParser.ParseCreateFunction(state, options.Aggregate, options.CreateOrLabel, options.Definer),
            Keyword.PROCEDURE or Keyword.PROC => DdlParser.ParseCreateProcedure(state, options.CreateOrLabel, options.Definer),
            Keyword.VIEW => DdlParser.ParseCreateView(state, options.CreateOrLabel, options.Definer, options.ViewAlgorithm, options.SecurityContext, options.TemporaryLabel, options.Recursive),
            Keyword.INDEX or Keyword.UNIQUE or Keyword.FULLTEXT or Keyword.SPATIAL or Keyword.CLUSTERED or Keyword.NONCLUSTERED => DdlParser.ParseCreateIndex(state, options.CreateOrLabel),
            _ => throw state.ExpectedException(options.AllSupportedTypes),
        };
    }

    protected override Statement ParseNormalStatement(ParserState state, Token token)
    {
        return token switch
        {
            MsBatchSeparatorToken mbsToken => ParseBatchSeparator(state, mbsToken),
            _ => base.ParseNormalStatement(state, token)
        };
    }

    protected override Statement ParseKeywordStatement(ParserState state, Word first)
    {
        return first.Keyword switch
        {
            Keyword.EXEC or Keyword.EXECUTE => ParseExecute(state, first.Keyword),
            Keyword.BREAK => ParseBreak(state),
            Keyword.CONTINUE => ParseContinue(state),
            Keyword.SAVE => ParseSaveTransaction(state),
            Keyword.PRINT => ParsePrint(state),
            Keyword.THROW => ParseThrow(state),
            Keyword.RAISERROR => ParseRaiseError(state),
            Keyword.REFRESH => throw ParserState.ExpectedException("SQL statement".Italic(), first),
            _ => base.ParseKeywordStatement(state, first)
        };
    }

    protected override bool ConsumeTerminators(ParserState state, ref ParserRunState runState, bool expectingTerminator)
    {
        expectingTerminator = base.ConsumeTerminators(state, ref runState, expectingTerminator);
        if (state.Peek() is MsBatchSeparatorToken)
        {
            return false; // MsBatchSeparatorToken acts as a terminator itself
        }
        return expectingTerminator;
    }

    protected override bool ParseNextStatement(ParserState state, ref ParserRunState runState, SqlValueList<Statement> statements)
    {
        Statement statement = ParseStatement(state);
        statements.Add(statement);
        return statement is not MsBatchSeparatorStatement;
    }

    protected override Statement ParseAlter(ParserState state)
    {
        state.ExpectKeyword(Keyword.ALTER);

        return state.Peek() switch
        {
            Word { Keyword: Keyword.TABLE } => TableParser.ParseAlter(state),
            _ => throw state.ExpectedException("TABLE")
        };
    }

    private static MsBatchSeparatorStatement ParseBatchSeparator(ParserState state, MsBatchSeparatorToken token)
    {
        state.Next(); // Consume the GO token
        return new MsBatchSeparatorStatement(token.Count);
    }

    /// <summary>
    /// Parses a T-SQL <c>EXEC[UTE]</c> statement. Supports the procedure-call
    /// form (with optional return-status capture, named or positional
    /// arguments, and <c>OUTPUT</c>/<c>DEFAULT</c> argument modifiers) and the
    /// parenthesized dynamic-SQL form <c>EXEC (string_expression)</c>.
    /// </summary>
    private MsExecute ParseExecute(ParserState state, Keyword kw)
    {
        state.Next(); // Consume the EXEC or EXECUTE keyword
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
        if (state.Peek() is Word { Sigil: SigilKind.Variable } && state.PeekNthIs<Equal>(1))
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
    /// <code><![CDATA[
    /// BEGIN TRY
    ///     statements
    /// END TRY
    /// BEGIN CATCH
    ///     statements
    /// END CATCH</c> block.
    /// ]]></code>
    /// </summary>
    private MsTryCatch ParseTryCatch(ParserState state)
    {
        state.ExpectKeywordsAll(Keyword.BEGIN, Keyword.TRY);
        var tryEnder = new EndSubstatements()
        {
            GetFinished = s => s.PeekKeywordsAll(Keyword.END, Keyword.TRY),
            GetEofException = s => s.ExpectedException("END TRY"),
        };
        SqlValueList<Statement> tryStatements = Parse(state, tryEnder);
        state.ExpectKeywordsAll(Keyword.END, Keyword.TRY);
        state.ExpectKeywordsAll(Keyword.BEGIN, Keyword.CATCH);
        var catchEnder = new EndSubstatements()
        {
            GetFinished = s => s.PeekKeywordsAll(Keyword.END, Keyword.CATCH),
            GetEofException = s => s.ExpectedException("END CATCH"),
        };
        SqlValueList<Statement> catchStatements = Parse(state, catchEnder);
        state.ExpectKeywordsAll(Keyword.END, Keyword.CATCH);
        return new MsTryCatch(tryStatements, catchStatements);
    }

    /// <summary>
    /// <code><![CDATA[
    /// BEGIN { TRAN | TRANSACTION } [ name ]
    /// ]]></code>
    /// </summary>
    public MsBeginTransaction ParseBeginTransaction(ParserState state)
    {
        state.ExpectKeyword(Keyword.BEGIN);
        TransactionNoun noun = ControlFlowParser.ParseRequiredTransactionNoun(state);
        Identifier? name = ControlFlowParser.ParseTransactionName(state, noun);
        return new MsBeginTransaction(noun) { Name = name };
    }

    /// <summary>
    /// <code><![CDATA[
    /// SAVE { TRAN | TRANSACTION } savepoint_name
    /// ]]>
    /// </summary>
    public MsSaveTransaction ParseSaveTransaction(ParserState state)
    {
        state.ExpectKeyword(Keyword.SAVE);
        TransactionNoun noun = ControlFlowParser.ParseRequiredTransactionNoun(state);
        Identifier name = ComponentParser.ParseIdentifier(state, "savepoint_name");
        return new MsSaveTransaction(noun, name);
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
        if (IsStatementTerminator(state.Peek(), state))
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

    public override bool IsStatementTerminator(Token token, ParserState state)
    {
        return token switch
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
