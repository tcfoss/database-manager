namespace TcfOss.DatabaseManager.Core.Parsing;

[Flags]
public enum RoutineParameterModeSupport
{
    None = 0,
    In = 1,
    Out = 2,
    InOut = 4,
    Variadic = 8,
    Basic = In | Out | InOut,
    All = Basic | Variadic
}

[Flags]
public enum RoutineParameterDefaultSyntax
{
    None = 0,
    DefaultKeyword = 1,
    Equals = 2,
    Both = DefaultKeyword | Equals
}

[Flags]
public enum RoutineParameterDefaultKind
{
    None = 0,
    Value = 1,
    Expression = 2,
    Both = Value | Expression
}

public enum RoutineParameterNameMode
{
    OptionalIdentifier,
    RequiredIdentifier,
    RequiredVariable
}

public sealed record RoutineParamsSupportStyle(
    RoutineParameterNameMode NameMode,
    RoutineParameterModeSupport Modes,
    RoutineParameterDefaultSyntax DefaultSyntax,
    RoutineParameterDefaultKind DefaultKinds,
    bool AllowSqlServerOptions)
{
    public bool AllowPgTypeOf { get; init; }

    public static RoutineParamsSupportStyle KitchenSink { get; } = new(
        RoutineParameterNameMode.OptionalIdentifier,
        RoutineParameterModeSupport.All,
        RoutineParameterDefaultSyntax.Both,
        RoutineParameterDefaultKind.Both,
        AllowSqlServerOptions: true);

    public static RoutineParamsSupportStyle MySqlFunction { get; } = new(
        RoutineParameterNameMode.RequiredIdentifier,
        RoutineParameterModeSupport.Basic,
        RoutineParameterDefaultSyntax.DefaultKeyword,
        RoutineParameterDefaultKind.Both,
        AllowSqlServerOptions: false);

    public static RoutineParamsSupportStyle MySqlProcedure { get; } = new(
        RoutineParameterNameMode.RequiredIdentifier,
        RoutineParameterModeSupport.Basic,
        RoutineParameterDefaultSyntax.DefaultKeyword,
        RoutineParameterDefaultKind.Both,
        AllowSqlServerOptions: false);

    public static RoutineParamsSupportStyle PostgreSqlFunction { get; } = new(
        RoutineParameterNameMode.OptionalIdentifier,
        RoutineParameterModeSupport.All,
        RoutineParameterDefaultSyntax.Both,
        RoutineParameterDefaultKind.Expression,
        AllowSqlServerOptions: false);

    public static RoutineParamsSupportStyle PostgreSqlProcedure { get; } = new(
        RoutineParameterNameMode.OptionalIdentifier,
        RoutineParameterModeSupport.All,
        RoutineParameterDefaultSyntax.Both,
        RoutineParameterDefaultKind.Expression,
        AllowSqlServerOptions: false);

    public static RoutineParamsSupportStyle MsSqlFunction { get; } = new(
        RoutineParameterNameMode.RequiredVariable,
        RoutineParameterModeSupport.None,
        RoutineParameterDefaultSyntax.Equals,
        RoutineParameterDefaultKind.Value,
        AllowSqlServerOptions: true);

    public static RoutineParamsSupportStyle MsSqlProcedure { get; } = new(
        RoutineParameterNameMode.RequiredVariable,
        RoutineParameterModeSupport.None,
        RoutineParameterDefaultSyntax.Equals,
        RoutineParameterDefaultKind.Value,
        AllowSqlServerOptions: true);
}
