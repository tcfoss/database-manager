using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
namespace TcfOss.DatabaseManager.Core.App;

// Service provider also provided for library usage
// ReSharper disable UnusedMember.Global
public class AppServiceProvider
{
    [ThreadStatic]
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider ServiceProvider
    {
        protected get
        {
            GuardServiceProvider();
            return _serviceProvider;
        }
        set
        {
            _serviceProvider = value;
        }
    }

    [MemberNotNull(nameof(_serviceProvider))]
    private static void GuardServiceProvider()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider has not been initialized.");
        }
    }

    public static IDownloadSchema SchemaDownloader
    {
        get
        {
            GuardServiceProvider();
            return _serviceProvider.GetRequiredService<IDownloadSchema>();
        }
    }

    public static IComputeChanges ChangeComputer
    {
        get
        {
            GuardServiceProvider();
            return _serviceProvider.GetRequiredService<IComputeChanges>();
        }
    }

    public static IGetUnappliedRefactors RefactorLoader
    {
        get
        {
            GuardServiceProvider();
            return _serviceProvider.GetRequiredService<IGetUnappliedRefactors>();
        }
    }

    public static IServiceScopeFactory ScopeFactory
    {
        get
        {
            GuardServiceProvider();
            return _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        }
    }

    public static ILogger<T> GetLogger<T>()
    {
        GuardServiceProvider();
        return _serviceProvider.GetRequiredService<ILogger<T>>();
    }
}
