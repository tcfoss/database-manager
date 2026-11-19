using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public static class NormalizationExtensions
{
    public static Direction? Clean(this Direction? direction, bool removeOptionalElements)
    {
        if (!removeOptionalElements)
        {
            return direction;
        }
        return direction == Direction.Descending ? Direction.Descending : null;
    }
}
