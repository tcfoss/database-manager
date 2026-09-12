using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.App;

// ReSharper disable once ClassNeverInstantiated.Global
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
