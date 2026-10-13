using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public class GenericSqlFormatter(GenericConfig config, IWriteFiles fileWriter, IParseText textParser, IFunctionNameProvider functionNameProvider, ILogger<GenericSqlFormatter> logger)
    : SqlFormatter<GenericConfig>(config, fileWriter, textParser, functionNameProvider, logger);
