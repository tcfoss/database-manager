using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public readonly record struct IndexInfo()
{
    public required Identifier ColumnName { get; init; }
    public uint? SubPart { get; init; }
    public Direction? Collation { get; init; }
    public required string IndexType { get; init; }
    public required string IndexComment { get; init; }
    public required string IndexName { get; init; }
    public required uint SeqInIndex { get; init; }

    public (IndexMethod? Method, Comment? Comment) GetIndexTypeAndComment(ILexer lexer, IParser parser)
    {
        var indexMethodState = new ParserState([.. lexer.Tokenize(IndexType)]);
        IndexMethod indexMethod = parser.TableParser.ParseIndexMethod(indexMethodState);

        Comment? comment = null;
        if (IndexComment != "")
        {
            comment = new Comment(IndexComment);
        }

        return (indexMethod, comment);
    }
}
