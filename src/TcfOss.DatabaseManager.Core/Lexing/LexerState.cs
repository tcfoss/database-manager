using System.Buffers;
using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing;

public ref struct LexerState
{
    private readonly ReadOnlySpan<char> _characters;
    private int _line = 1;
    private int _column = 1;

    public int Position { get; private set; } = 0;
    public string? IdentifierTerminator { get; set; }

    public readonly Location Location
    {
        get
        {
            return new Location(_line, _column, Position);
        }
    }

    public LexerState(ReadOnlySpan<char> characters)
    {
        _characters = characters;
    }

    private LexerState(ReadOnlySpan<char> characters, int position, int line, int column)
    {
        _characters = characters;
        Position = position;
        _line = line;
        _column = column;
    }

    public readonly char Peek()
    {
        if (Position >= _characters.Length)
        {
            return CharSymbols.EndOfFile;
        }
        return _characters[Position];
    }

    public readonly char PeekNth(int n)
    {
        if (Position + n >= _characters.Length)
        {
            return CharSymbols.EndOfFile;
        }
        return _characters[Position + n];
    }

    public char Next()
    {
        char next = Peek();
        Position++;

        if (Position >= _characters.Length)
        {
            return next;
        }

        if (next == CharSymbols.NewLine)
        {
            NewLine();
        }
        else
        {
            MoveColumn();
        }

        return next;
    }

    private void Advance(int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (_characters[Position + i] == CharSymbols.NewLine)
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }
        Position += n;
    }

    public int AdvanceThroughDigits()
    {
        int start = Position;
        while (Position < _characters.Length && char.IsDigit(_characters[Position]))
        {
            Position++;
        }
        _column += Position - start;
        return Position - start;
    }

    public void AdvanceToEndOfLine()
    {
        ReadOnlySpan<char> remaining = _characters[Position..];

        int index = remaining.IndexOfAny(CharSymbols.NewLine, CharSymbols.EndOfFile);

        if (index < 0)
        {
            index = remaining.Length;
        }
        Position += index;
        _column += index;
    }

    public ReadOnlySpan<char> TakeWhile(Func<char, bool> predicate)
    {
        int start = Position;
        while (Position < _characters.Length && predicate(_characters[Position]))
        {
            if (_characters[Position] == CharSymbols.NewLine)
            {
                NewLine();
            }
            else
            {
                MoveColumn();
            }
            Position++;
        }
        return _characters[start..Position];
    }

    public ReadOnlySpan<char> TakeWhileDigit()
    {
        int start = Position;
        while ((uint)Position < (uint)_characters.Length && char.IsDigit(_characters[Position]))
        {
            Position++;
        }
        _column += Position - start;
        return _characters[start..Position];
    }

    public ReadOnlySpan<char> TakeWhileHexDigit()
    {
        int start = Position;
        while ((uint)Position < (uint)_characters.Length && char.IsAsciiHexDigit(_characters[Position]))
        {
            Position++;
        }
        _column += Position - start;
        return _characters[start..Position];
    }

    public ReadOnlySpan<char> TakeWhileIdentifierPart(SearchValues<char> asciiCharacters, Func<char, bool> isIdentifierPart)
    {
        ReadOnlySpan<char> remaining = _characters[Position..];
        int length = 0;
        while (length < remaining.Length)
        {
            int index = remaining[length..].IndexOfAnyExcept(asciiCharacters);
            if (index < 0)
            {
                length = remaining.Length;
                break;
            }
            length += index;
            char character = remaining[length];
            if (char.IsAscii(character) || !isIdentifierPart(character))
            {
                break;
            }
            length++;
        }
        if (IdentifierTerminator is { Length: > 0 } terminator)
        {
            int searchLength = length + Math.Min(remaining.Length - length, terminator.Length - 1);
            int terminatorIndex = remaining[..searchLength].IndexOf(terminator.AsSpan());
            if (terminatorIndex >= 0)
            {
                length = terminatorIndex;
            }
        }
        ReadOnlySpan<char> result = remaining[..length];
        Position += length;
        _column += length;
        return result;
    }

    public bool TakeIfSequence(ReadOnlySpan<char> chars)
    {
        if (Position + chars.Length > _characters.Length)
        {
            return false;
        }

        if (!_characters.Slice(Position, chars.Length).SequenceEqual(chars))
        {
            return false;
        }

        Advance(chars.Length);
        return true;
    }

    public bool TakeIfSequenceCaseInsensitive(ReadOnlySpan<char> chars)
    {
        if (Position + chars.Length > _characters.Length)
        {
            return false;
        }

        if (!_characters.Slice(Position, chars.Length).Equals(chars, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Advance(chars.Length);
        return true;
    }

    public readonly ReadOnlySpan<char> SpanFrom(int start)
    {
        if (Position > _characters.Length)
        {
            return _characters[start..];
        }
        return _characters[start..Position];
    }

    public readonly ReadOnlySpan<char> SpanFrom(int start, int end)
    {
        return _characters[start..end];
    }

    private void NewLine()
    {
        _line++;
        _column = 1;
    }

    private void MoveColumn()
    {
        _column++;
    }

    public readonly LexerState Clone()
    {
        return new LexerState(_characters, Position, _line, _column)
        {
            IdentifierTerminator = IdentifierTerminator
        };
    }

    internal static readonly SearchValues<char> s_standardIdentifierCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");
}
