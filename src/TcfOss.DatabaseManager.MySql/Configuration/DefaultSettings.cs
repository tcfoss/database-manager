using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.MySql.Configuration;

public static class DefaultSettings
{
    public static string DefaultEngine { get; } = "InnoDB";

    public static string GetDefaultCharacterSet(SqlDialect dialect, Version serverVersion)
    {
        if (dialect == SqlDialect.MariaDb && serverVersion >= new Version(10, 1))
        {
            return "utf8mb4";
        }
        if (dialect == SqlDialect.MySql && serverVersion.Major >= 8)
        {
            return "utf8mb4";
        }
        return "latin1";
    }

    private static CharacterSetInfo[] GetCharacterSetInfoMySql(Version version)
    {
        if (version.Major >= 8)
        {
            return
            [
                new CharacterSetInfo("utf8mb4", ["utf8mb4_0900_ai_ci", "utf8mb4_bin"]),
                new CharacterSetInfo("latin1", ["latin1_swedish_ci", "latin1_bin"]),
                new CharacterSetInfo("ascii", ["ascii_general_ci", "ascii_bin"]),
            ];
        }
        else
        {
            return
            [
                new CharacterSetInfo("utf8mb4", ["utf8mb4_general_ci", "utf8mb4_unicode_ci", "utf8mb4_bin"]),
                new CharacterSetInfo("utf8", ["utf8_general_ci", "utf8_unicode_ci", "utf8_bin"]),
                new CharacterSetInfo("latin1", ["latin1_swedish_ci", "latin1_bin"]),
                new CharacterSetInfo("ascii", ["ascii_general_ci", "ascii_bin"]),
            ];
        }
    }

    private static CharacterSetInfo[] GetCharacterSetInfoMariaDb(Version version)
    {
        if (version >= new Version(11, 4, 2))
        {
            // UCA 14.0.0 collation made default for utf8mb4 in 11.4.2
            return
            [
                new CharacterSetInfo("utf8mb4", ["utf8mb4_uca1400_ai_ci", "utf8mb4_general_ci", "utf8mb4_unicode_ci", "utf8mb4_bin"]),
                new CharacterSetInfo("utf8mb3", ["utf8mb3_general_ci", "utf8mb3_unicode_ci", "utf8mb3_bin"]),
                new CharacterSetInfo("latin1", ["latin1_swedish_ci", "latin1_bin"]),
                new CharacterSetInfo("ascii", ["ascii_general_ci", "ascii_bin"]),
            ];
        }
        else if (version >= new Version(10, 10))
        {
            // UCA 14.0.0 collations added in 10.10, but not yet made default
            return
            [
                new CharacterSetInfo("utf8mb4", ["utf8mb4_general_ci", "utf8mb4_uca1400_ai_ci", "utf8mb4_unicode_ci", "utf8mb4_bin"]),
                new CharacterSetInfo("utf8mb3", ["utf8mb3_general_ci", "utf8mb3_unicode_ci", "utf8mb3_bin"]),
                new CharacterSetInfo("latin1", ["latin1_swedish_ci", "latin1_bin"]),
                new CharacterSetInfo("ascii", ["ascii_general_ci", "ascii_bin"]),
            ];
        }
        else
        {
            return
            [
                new CharacterSetInfo("utf8mb4", ["utf8mb4_general_ci", "utf8mb4_unicode_ci", "utf8mb4_bin"]),
                new CharacterSetInfo("utf8mb3", ["utf8mb3_general_ci", "utf8mb3_unicode_ci", "utf8mb3_bin"]),
                new CharacterSetInfo("latin1", ["latin1_swedish_ci", "latin1_bin"]),
                new CharacterSetInfo("ascii", ["ascii_general_ci", "ascii_bin"]),
            ];
        }
    }

    private static CharacterSetInfo[] GetCharacterSetInfo(SqlDialect dialect, Version version)
    {
        return dialect switch
        {
            SqlDialect.MySql => GetCharacterSetInfoMySql(version),
            SqlDialect.MariaDb => GetCharacterSetInfoMariaDb(version),
            _ => throw new NotSupportedException($"Dialect {dialect} is not supported.")
        };
    }

    public static Dictionary<string, CharacterSetSpec> GetCharacterSets(SqlDialect dialect, Version version)
    {
        CharacterSetInfo[] characterSetInfo = GetCharacterSetInfo(dialect, version);

        var characterSetDict = new Dictionary<string, CharacterSetSpec>();
        foreach (CharacterSetInfo info in characterSetInfo)
        {
            characterSetDict[info.CharacterSet] = new CharacterSetSpec()
            {
                CharacterSet = info.CharacterSet,
                DefaultCollation = info.Collations[0],
                Collations = [.. info.Collations]
            };
        }

        return characterSetDict;
    }

    private record struct CharacterSetInfo(string CharacterSet, string[] Collations);
}
