using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Parsing;

public abstract record KeywordSearchCondition()
{
    public record RequiredKeyword(Keyword Keyword) : KeywordSearchCondition;

    public record OneOfKeywords(params Keyword[] Keywords) : KeywordSearchCondition;

    public record OptionalKeywordSequence(params Keyword[] Keywords) : KeywordSearchCondition;

    public record OptionalKeyword(Keyword Keyword) : KeywordSearchCondition;

    public static implicit operator KeywordSearchCondition(Keyword keyword)
    {
        return new RequiredKeyword(keyword);
    }


    public static RequiredKeyword Required(Keyword keyword)
    {
        return new RequiredKeyword(keyword);
    }

    public static OneOfKeywords OneOf(params Keyword[] keywords)
    {
        return new OneOfKeywords(keywords);
    }

    public static OptionalKeyword Optional(Keyword keyword)
    {
        return new OptionalKeyword(keyword);
    }

    public static OptionalKeywordSequence Optionals(params Keyword[] keywords)
    {
        return new OptionalKeywordSequence(keywords);
    }
}
