using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class CreateObjectOptions
{
    public CreateOrLabel? CreateOrLabel { get; init; }
    public ViewAlgorithm? ViewAlgorithm { get; init; }
    public SecurityContext? SecurityContext { get; init; }
    public Definer? Definer { get; init; }
    public bool Aggregate { get; init; }
    public TemporaryLabel? TemporaryLabel { get; init; }
    public bool Recursive { get; init; }

    public string[] AllSupportedTypes { get; init; } = ["TABLE", "TRIGGER", "CONSTRAINT TRIGGER", "VIEW", "MATERIALIZED VIEW", "FUNCTION", "PROCEDURE", "EVENT", "INDEX", "TYPE", "DOMAIN"];
}
