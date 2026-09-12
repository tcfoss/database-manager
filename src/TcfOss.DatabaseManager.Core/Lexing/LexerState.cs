using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Lexing;

public ref struct LexerState
{
    private readonly ReadOnlySpan<char> _characters;
    private int _line = 1;
    private int _column = 1;
    private bool _finished = false;

    public int Position { get; private set; } = 0;

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

    private LexerState(ReadOnlySpan<char> characters, int position, int line, int column, bool finished)
    {
        _characters = characters;
        Position = position;
        _line = line;
        _column = column;
        _finished = finished;
    }

    public readonly char Peek()
    {
        if (_finished || Position >= _characters.Length)
        {
            return CharSymbols.EndOfFile;
        }
        return _characters[Position];
    }

    public readonly char PeekNth(int n)
    {
        if (_finished || Position + n >= _characters.Length)
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
            _finished = true;
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

    public ReadOnlySpan<char> TakeWhileTrustNoNewLines(Func<char, bool> predicate)
    {
        int start = Position;
        while (Position < _characters.Length && predicate(_characters[Position]))
        {
            Position++;
        }
        _column += Position - start;
        return _characters[start..Position];
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
        return new LexerState(_characters, Position, _line, _column, _finished);
    }
}
