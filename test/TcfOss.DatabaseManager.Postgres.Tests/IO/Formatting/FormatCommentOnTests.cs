namespace TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting;

public class FormatCommentOnTests
{
    [Theory]
    [InlineData("COMMENT ON TABLE app.items IS 'it''''s'", "COMMENT ON TABLE \"app\".\"items\" IS 'it''''s'")]
    [InlineData("COMMENT ON TABLE app.items IS $tag$it''s; opaque$tag$", "COMMENT ON TABLE \"app\".\"items\" IS $tag$it''s; opaque$tag$")]
    [InlineData("COMMENT ON TABLE app.items IS $$text$$", "COMMENT ON TABLE \"app\".\"items\" IS $$text$$")]
    [InlineData("COMMENT ON COLUMN app.items.id IS ''", "COMMENT ON COLUMN \"app\".\"items\".\"id\" IS ''")]
    [InlineData("COMMENT ON FUNCTION app.f(INT, OUT result TEXT) IS 'function'", "COMMENT ON FUNCTION \"app\".\"f\"(INT, OUT \"result\" TEXT) IS 'function'")]
    [InlineData("COMMENT ON FUNCTION app.f() IS 'empty signature'", "COMMENT ON FUNCTION \"app\".\"f\"() IS 'empty signature'")]
    [InlineData("COMMENT ON PROCEDURE app.p IS 'no signature'", "COMMENT ON PROCEDURE \"app\".\"p\" IS 'no signature'")]
    [InlineData("COMMENT ON TRIGGER audit ON app.items IS NULL", "COMMENT ON TRIGGER \"audit\" ON \"app\".\"items\" IS NULL")]
    public void Comments_FormatWithoutLosingTargetOrText(string sql, string expected)
    {
        using var formatter = Helpers.CreateFormatter();

        string actual = formatter.GetFormatted(sql);

        Assert.Equal(expected, actual.TrimEnd(';'));
    }
}
