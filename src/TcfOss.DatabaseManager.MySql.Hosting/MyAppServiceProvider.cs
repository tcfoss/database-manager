using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.Hosting;

public class MyAppServiceProvider : AppServiceProvider
{
    public static ILoadFsDefinition<MyDefinition> FilesystemDefinitionLoader
    {
        get
        {
            return ServiceProvider.GetRequiredService<ILoadFsDefinition<MyDefinition>>();
        }
    }

    public static ILoadDbDefinition<MyDefinition> DatabaseDefinitionLoader
    {
        get
        {
            return ServiceProvider.GetRequiredService<ILoadDbDefinition<MyDefinition>>();
        }
    }
}
