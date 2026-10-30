using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

public interface IHaveOptionalPrecision
{
    public uint? Precision { get; }

    public void WritePrecision(SqlTextWriter writer)
    {
        if (Precision != null)
        {
            writer.WriteSql($"({Precision})");
        }
    }
}