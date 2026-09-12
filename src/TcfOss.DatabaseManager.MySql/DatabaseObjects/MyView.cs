using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public record MyView(ObjectIdentifier Name, Select Body)
    : View(Name, Body)
{
    public required Definer Definer { get; init; }
    public required SecurityContext SecurityContext { get; init; }
    public required ViewAlgorithm? Algorithm { get; init; }
    public ViewCheckOption? CheckOption { get; init; }

    public Select? NormalizedBody { get; init; }

    public virtual bool Equals(MyView? other)
    {
        if (other == null)
        {
            return false;
        }

        Select comparableBody = NormalizedBody ?? Body;
        Select otherComparableBody = other.NormalizedBody ?? other.Body;

        return Name == other.Name
            && Definer == other.Definer
            && SecurityContext == other.SecurityContext
            && Algorithm == other.Algorithm
            && CheckOption == other.CheckOption
            && comparableBody == otherComparableBody;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Definer, SecurityContext, Algorithm, CheckOption);
    }

    public override CreateView ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        Select body = manager?.Formatting.ViewPreferNormalizedBody == true && NormalizedBody != null ? NormalizedBody : Body;
        return new CreateView(Name.ToObjectName(includeSchema ? 2 : 1), body)
        {
            Definer = Definer,
            ViewAlgorithm = Algorithm,
            SecurityContext = SecurityContext,
            ViewCheckOption = CheckOption,
            Meta = new MetaData { RawText = RawBodyText }
        };
    }
}
