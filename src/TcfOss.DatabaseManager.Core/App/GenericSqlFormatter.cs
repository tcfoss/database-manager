using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public class GenericSqlFormatter(ConfigGeneric config, IWriteFiles fileWriter, IParseText textParser, IFunctionNameProvider functionNameProvider, ILogger<GenericSqlFormatter> logger)
    : SqlFormatter<ConfigGeneric>(config, fileWriter, textParser, functionNameProvider, logger);
