using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

[StringEnum("MICROSECOND")]
[StringEnum("SECOND")]
[StringEnum("MINUTE")]
[StringEnum("HOUR")]
[StringEnum("DAY")]
[StringEnum("WEEK")]
[StringEnum("MONTH")]
[StringEnum("QUARTER")]
[StringEnum("YEAR")]
[StringEnum("SECOND_MICROSECOND")]
[StringEnum("MINUTE_MICROSECOND")]
[StringEnum("MINUTE_SECOND")]
[StringEnum("HOUR_MICROSECOND")]
[StringEnum("HOUR_SECOND")]
[StringEnum("HOUR_MINUTE")]
[StringEnum("DAY_MICROSECOND")]
[StringEnum("DAY_SECOND")]
[StringEnum("DAY_MINUTE")]
[StringEnum("DAY_HOUR")]
[StringEnum("YEAR_MONTH")]
public sealed partial class DateTimeUnit;
