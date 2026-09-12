using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace TcfOss.DatabaseManager.Core.IO;

public static class StringBuilderPool
{
    private static readonly ObjectPool<StringBuilder> s_pool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

    public static StringBuilder Get()
    {
        return s_pool.Get();
    }

    public static string Return(StringBuilder builder)
    {
        try
        {
            return builder.ToString();
        }
        finally
        {
            s_pool.Return(builder);
        }
    }
}
