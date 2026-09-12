namespace TcfOss.DatabaseManager.Core.Configuration;

public class ValidationSettings
{
    public bool AllowUniqueOnColumn { get; init; }
    public bool AllowCheckOnColumn { get; init; }

    public bool AllowNamedColumnDefault { get; init; }
    public bool AllowNamedColumnUnique { get; init; }
    public bool AllowNamedColumnNullability { get; init; }
    public bool AllowNamedColumnCheck { get; init; }

    public bool AllowCreateTableAsSelect { get; init; }
    public bool AllowCreateTableNoColumns { get; init; }
    public bool AllowMissingSecurityContext { get; init; }
    public bool AllowMissingDefiner { get; init; }
    public bool AllowImplicitDefiner { get; init; }
}
