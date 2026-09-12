using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;

namespace TcfOss.DatabaseManager.MariaDb.Tests.DefinitionBuilding;

// ReSharper disable once UnusedMember.Global
public class MaRelaxedDefinitionBuilderTests : MaDefinitionBuilderTests
{
    protected override bool Relaxed => true;
    protected override Func<MyConfig, ILogger<MyDefinitionBuilder>, SourceManager, MyDefinitionBuilder> DefinitionBuilderFactory => (config, logger, sourceManager) => new MyRelaxedDefinitionBuilder(config, sourceManager, new MyFunctionNameProvider(), logger);
}
