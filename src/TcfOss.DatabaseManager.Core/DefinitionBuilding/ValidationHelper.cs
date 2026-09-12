using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Extensions;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public partial class ValidationHelper(ILogger logger)
{
    private readonly ILogger _logger = logger;

    private List<Identifier>? ExtractColumnNames(NamedKeyPartList source, string? extractionReason)
    {
        s_logExtractingKeyParts(_logger, source.Name?.Name, extractionReason, null);

        if (!source.Columns.SafeAny())
        {
            s_logNoKeyParts(_logger, source.Name?.Name, extractionReason, null);
            return null;
        }

        var columnNames = new List<Identifier>();
        foreach (KeyPart keyPart in source.Columns)
        {
            if (keyPart is KeyPart.Column columnKeyPart)
            {
                columnNames.Add(columnKeyPart.Name);
            }
            else
            {
                s_logUnusableKeyPart(_logger, keyPart.GetType().Name, source.Name?.Name, null);
                break;
            }
        }

        if (columnNames.Count > 0)
        {
            return columnNames;
        }
        else
        {
            s_logNoKeyParts(_logger, source.Name?.Name, extractionReason, null);
            return null;
        }
    }

    private static bool IsColumnListMatch(List<Identifier> requiredColumns, List<Identifier>? potentialMatchColumnNames)
    {
        if (potentialMatchColumnNames == null)
        {
            return false;
        }
        if (potentialMatchColumnNames.Count < requiredColumns.Count)
        {
            return false;
        }

        for (int i = 0; i < requiredColumns.Count; i++)
        {
            if (requiredColumns[i].Name != potentialMatchColumnNames[i].Name)
            {
                return false;
            }
        }
        return true;
    }

    public NamedKeyPartList? GetValidMatchingKeyPartList(List<Identifier> requiredColumns, IEnumerable<NamedKeyPartList> potentialMatches, string? extractionReason)
    {
        foreach (NamedKeyPartList potentialMatch in potentialMatches)
        {
            List<Identifier>? potentialMatchColumnNames = ExtractColumnNames(potentialMatch, extractionReason);
            if (IsColumnListMatch(requiredColumns, potentialMatchColumnNames))
            {
                return potentialMatch;
            }
        }
        return null;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Extracting key parts from index '{IndexName}' for '{ExtractionReason}'")]
    private static partial void s_logExtractingKeyParts(ILogger logger, string? indexName, string? extractionReason, Exception? ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Found unusable key part of type '{KeyPartType}' in index '{IndexName}'. Returning only previous key parts.")]
    private static partial void s_logUnusableKeyPart(ILogger logger, string? keyPartType, string? indexName, Exception? ex);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "No usable key parts found in index '{IndexName}' for '{ExtractionReason}'.")]
    private static partial void s_logNoKeyParts(ILogger logger, string? indexName, string? extractionReason, Exception? ex);
}
