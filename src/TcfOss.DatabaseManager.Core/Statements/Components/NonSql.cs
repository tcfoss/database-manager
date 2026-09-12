using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record NonSql() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public abstract record Whitespace(int Number) : NonSql;
    public record Spaces(int Number) : Whitespace(Number)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            for (int i = 0; i < Number; i++)
            {
                writer.Write(" ");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        { }
    }

    public record Tabs(int Number = 1) : Whitespace(Number)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            for (int i = 0; i < Number; i++)
            {
                writer.Write("\t");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        { }
    }

    public record Newlines(int Number = 1) : Whitespace(Number)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            for (int i = 0; i < Number; i++)
            {
                writer.Write("\n");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            for (int i = 0; i < Number; i++)
            {
                writer.Write("\n");
            }
        }
    }

    public abstract record Comment() : NonSql;

    public record BlockComment(string Body) : Comment
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"/*{Body}*/");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatSql(writer, manager, originalSpaces: 0);
        }

        public void FormatSql(SqlTextWriter writer, FormatManager manager, int originalSpaces)
        {
            string[] lines = Body.Split(["\n", "\r\n"], StringSplitOptions.None);

            writer.Write(writer.GetCurrentLineLength() > 0
                ? new string(' ', originalSpaces)
                : manager.Indent);

            writer.Write("/*");
            writer.Write(lines[0]);

            if (lines.Length == 1)
            {
                writer.Write("*/");
                return;
            }

            int tabSize = manager.Formatting.TabSize;
            int currentSpaces = CountSpacesBeforeText(manager.Indent, tabSize);
            int delta = currentSpaces - originalSpaces;

            for (int i = 1; i < lines.Length; i++)
            {
                writer.Write("\n");
                string line = lines[i];
                int lineOriginalSpaces = CountSpacesBeforeText(line, tabSize);
                int lineNewSpaces = Math.Max(0, lineOriginalSpaces + delta);
                int leadingChars = CountLeadingWhitespaceChars(line);

                manager.Indenter.ResetIndent(lineNewSpaces);
                writer.Write(manager.Indent);
                writer.Write(line[leadingChars..]);

                if (i == lines.Length - 1)
                {
                    writer.Write("*/");
                }
            }

            // Restore the formatter's indent to what it was before we adjusted it.
            manager.Indenter.ResetIndent(currentSpaces);
        }

        private static int CountLeadingWhitespaceChars(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                i++;
            }
            return i;
        }
    }

    public record LineComment(string Body, string Prefix = "--") : Comment
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Prefix}{Body}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(writer.GetCurrentLineLength() > 0
                ? new string(' ', manager.Formatting.SpacesBeforeLineComment)
                : manager.Indent);
            writer.Write(Prefix);
            writer.Write(Body.TrimEnd('\n', '\r', '\0'));
            writer.WriteLine();
        }
    }


    public static int CountSpacesBeforeText(string text, int spacesPerTab)
    {
        int spaceCount = 0;
        foreach (char t in text)
        {
            if (t == ' ')
            {
                spaceCount++;
            }
            else if (t == '\t')
            {
                if (spaceCount % spacesPerTab != 0)
                {
                    spaceCount += spacesPerTab - (spaceCount % spacesPerTab);
                }
                else
                {
                    spaceCount += spacesPerTab;
                }
            }
            else
            {
                break;
            }
        }
        return spaceCount;
    }
}
