namespace TcfOss.DatabaseManager.Core.IO;

public class Indenter(int spacesPerTab = 4, bool preferTabs = false)
{
    private readonly int _spacesPerTab = spacesPerTab;
    private readonly bool _preferTabs = preferTabs;
    private readonly Dictionary<int, string> _cache = new() { [0] = "" };

    private int IndentCount { get; set; }

    public string IndentString { get; private set; } = "";

    private void UpdateIndent()
    {
        if (_cache.TryGetValue(IndentCount, out string? cached))
        {
            IndentString = cached;
            return;
        }

        if (_preferTabs)
        {
            int tabCount = IndentCount / _spacesPerTab;
            int remainingSpaces = IndentCount % _spacesPerTab;
            IndentString = new string('\t', tabCount) + new string(' ', remainingSpaces);
        }
        else
        {
            IndentString = new string(' ', IndentCount);
        }
        _cache[IndentCount] = IndentString;
    }

    public void IncreaseIndent(int? spaces = null)
    {
        if (spaces.HasValue)
        {
            IndentCount += spaces.Value;
        }
        else if (IndentCount % _spacesPerTab == 0)
        {
            IndentCount += _spacesPerTab;
        }
        else
        {
            IndentCount += _spacesPerTab - (IndentCount % _spacesPerTab);
        }
        UpdateIndent();
    }

    public void DecreaseIndent(int? spaces = null)
    {
        if (spaces.HasValue)
        {
            IndentCount -= spaces.Value;
        }
        else if (IndentCount % _spacesPerTab == 0)
        {
            IndentCount -= _spacesPerTab;
        }
        else
        {
            IndentCount -= IndentCount % _spacesPerTab;
        }
        if (IndentCount < 0)
        {
            IndentCount = 0;
        }
        UpdateIndent();
    }

    public void ResetIndent(int? spaces = null)
    {
        IndentCount = spaces ?? 0;
        UpdateIndent();
    }
}
