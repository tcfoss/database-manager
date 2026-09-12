using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MariaDb.App;

public static class MariaDbServiceRegistration
{
    public static void RegisterMariaDbServices(this IServiceCollection services, MyConfig config)
    {
        services.AddScoped<MyConfig>(_ => config);
        services.AddScoped<ConfigWithSchemaMapsBase<MySchemaMapping>>(_ => config);
        services.AddScoped<ILexer, MyLexer>();
        services.AddScoped<IParser, MyParser>();
        services.AddScoped<IParseText, TextParser>();
        services.AddPooledDbContextFactory<InfoSchemaContext>((_, options) =>
        {
            options.UseMySql(
                config.ConnectionString,
                ServerVersion.Create(config.Version, ServerType.MariaDb),
                mySqlOpts =>
                {
                    mySqlOpts.EnableRetryOnFailure();
                });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableDetailedErrors();
            if (config.Logging.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<InfoSchemaContext>(sp => sp.GetRequiredService<IDbContextFactory<InfoSchemaContext>>().CreateDbContext());
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<InfoSchemaContext>());
        services.AddScoped<IRetrieveDatabaseObjects, DatabaseComms.InfoSchemaRepo>();
        services.AddScoped<IRetrieveRawEntities, RawEntityRetriever>();
        services.AddScoped<ILoadFsDefinition<MyDefinition>, MyFsDefinitionLoader>();
        services.AddScoped<ILoadDbDefinition<MyDefinition>, MyDbDefinitionLoader>();
        services.AddScoped<IGetUnappliedRefactors, MyDbDefinitionLoader>();
        services.AddScoped<IGetUnappliedDeployScripts, MyDbDefinitionLoader>();
        services.AddScoped<IDownloadSchema, MyDownloadSchema>();
        services.AddScoped<IComputeChanges, MyChangeComputer>();
        services.AddScoped<IRunCommands, MyCommandRunner>();
        services.AddScoped<IReadSchemaMapFiles, DefinitionFileLoader<MySchemaMapping>>();
        services.AddScoped<IFormatSqlFiles, MySqlFormatter>();
        services.AddSingleton<IFunctionNameProvider, MyFunctionNameProvider>();
    }
}
