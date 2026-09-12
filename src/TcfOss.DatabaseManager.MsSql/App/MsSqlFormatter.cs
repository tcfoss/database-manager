using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.Configuration;

namespace TcfOss.DatabaseManager.MsSql.App;

public class MsSqlFormatter(
    MsConfig config,
    IWriteFiles fileWriter,
    IParseText textParser,
    IFunctionNameProvider functionNameProvider,
    ILogger<MsSqlFormatter> logger
) : SqlFormatter<MsConfig>(config, fileWriter, textParser, functionNameProvider, logger);
