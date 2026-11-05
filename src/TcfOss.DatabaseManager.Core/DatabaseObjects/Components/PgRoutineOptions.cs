using System.Globalization;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record PgRoutineOptions(Identifier Language)
{
    public SecurityContext Security { get; init; } = SecurityContext.Invoker;
    public SqlValueList<PgRoutineSetting> Settings { get; init; } = [];

    public abstract SqlValueList<PgStatementRoutineOption> ToStatementOptions();

#pragma warning disable CA1716
    public record Function(Identifier Language, float Cost, float Rows) : PgRoutineOptions(Language)
    {
        public PgRoutineVolatility Volatility { get; init; } = PgRoutineVolatility.Volatile;
        public bool Strict { get; init; }
        public PgRoutineParallel Parallel { get; init; } = PgRoutineParallel.Unsafe;

        public override SqlValueList<PgStatementRoutineOption> ToStatementOptions()
        {
            SqlValueList<PgStatementRoutineOption> options =
            [
                new PgStatementRoutineOption.Language(Language),
                new PgStatementRoutineOption.Volatility(Volatility),
                new PgStatementRoutineOption.NullHandling(Strict
                    ? PgRoutineNullHandling.Strict : PgRoutineNullHandling.CalledOnNullInput),
                new PgStatementRoutineOption.Security(Security),
                new PgStatementRoutineOption.Parallel(Parallel),
                new PgStatementRoutineOption.Cost(new Value.Number(Cost.ToString("R", CultureInfo.InvariantCulture), false))
            ];
            if (Rows > 0)
            {
                options.Add(new PgStatementRoutineOption.Rows(new Value.Number(Rows.ToString("R", CultureInfo.InvariantCulture), false)));
            }
            options.AddRange(Settings.Select(setting => new PgStatementRoutineOption.RoutineSetting(setting)));
            return options;
        }
    }

#pragma warning restore CA1716

    public record Procedure(Identifier Language) : PgRoutineOptions(Language)
    {
        public override SqlValueList<PgStatementRoutineOption> ToStatementOptions()
        {
            return
            [
                new PgStatementRoutineOption.Language(Language),
                new PgStatementRoutineOption.Security(Security),
                .. Settings.Select(setting => new PgStatementRoutineOption.RoutineSetting(setting))
            ];
        }
    }
}
