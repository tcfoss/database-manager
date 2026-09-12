using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;


public sealed record TableIndexMetadata(
    MyPrimaryKey? PrimaryKey,
    DatabaseComponentDict<MyUniqueKey> UniqueKeys,
    DatabaseComponentDict<MyKey> Keys);
