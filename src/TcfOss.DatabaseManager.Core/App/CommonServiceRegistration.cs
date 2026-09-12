using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public static class CommonServiceRegistration
{
    public static void RegisterCommonServices(this IHostApplicationBuilder builder, ConfigBase config)
    {
        builder.Services.AddScoped<ConfigBase>(_ => config);
        builder.Services.AddScoped<IParseText, TextParser>();
        builder.Services.AddSingleton<IWriteFiles, FileWriter>();
        builder.Services.AddSingleton<IReadEnvironmentVariables, EnvironmentVariableReader>();
        builder.Services.AddSingleton<SourceManager>();
    }
}
