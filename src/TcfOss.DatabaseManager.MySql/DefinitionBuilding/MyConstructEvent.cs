using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyConstructEvent(MyConfig config, MyAttributeNormalizer normalizeSql, SourceManager sourceManager)
{
    private readonly MyConfig _config = config;
    private readonly MyAttributeNormalizer _normalizeSql = normalizeSql;
    private readonly SourceManager _sourceManager = sourceManager;

    public MyEvent ConstructEvent(CreateEvent createEventStatement, ObjectIdentifier name, ExtendedQuoteStyle? accountQuoteStyle, SourceRef? sourceRef)
    {
        Definer definer = createEventStatement.Definer ?? new Definer(new Account.CurrentUser());
        definer = new Definer(_normalizeSql.NormalizeAccount(definer.Account, accountQuoteStyle));

        EventSchedule normalizedSchedule;
        if (createEventStatement.Schedule is EventSchedule.Every everySchedule)
        {
            LiteralValue? normalizedStart = null;
            LiteralValue? normalizedEnd = null;
            if (everySchedule.Start != null)
            {
                if (everySchedule.Start is LiteralValue { Value: Value.SingleQuotedString strVal } && DateTime.TryParse(strVal.Value, out DateTime parsedDate))
                {
                    normalizedStart = parsedDate.ToLiteralValueExpression();
                }
                else
                {
                    throw new DefinitionException.NotLiteralEventDate(name, everySchedule.Start.ToSql(), sourceRef);
                }
            }
            if (everySchedule.End != null)
            {
                if (everySchedule.End is LiteralValue { Value: Value.SingleQuotedString strVal } && DateTime.TryParse(strVal.Value, out DateTime parsedDate))
                {
                    normalizedEnd = parsedDate.ToLiteralValueExpression();
                }
                else
                {
                    throw new DefinitionException.NotLiteralEventDate(name, everySchedule.End.ToSql(), sourceRef);
                }
            }
            normalizedSchedule = everySchedule with
            {
                Start = normalizedStart,
                End = normalizedEnd
            };
        }
        else if (createEventStatement.Schedule is EventSchedule.At atSchedule)
        {
            if (atSchedule.Time is LiteralValue { Value: Value.SingleQuotedString strVal } && DateTime.TryParse(strVal.Value, out DateTime parsedDate))
            {
                LiteralValue normalizedTime = parsedDate.ToLiteralValueExpression();
                normalizedSchedule = new EventSchedule.At(normalizedTime);
            }
            else
            {
                throw new DefinitionException.NotLiteralEventDate(name, atSchedule.Time.ToSql(), sourceRef);
            }
        }
        else
        {
            // Those were the only options... This is just to satisfy the compiler.
            normalizedSchedule = createEventStatement.Schedule;
        }

        return new MyEvent(name, normalizedSchedule, createEventStatement.Body)
        {
            Definer = definer,
            OnCompletionPreserve = createEventStatement.OnCompletionPreserve ?? false,
            EventEnabledStatus = createEventStatement.EnabledStatus ?? _config.AttributeDefaults.EventEnabledStatus,
            Comment = createEventStatement.Comment,
            RawBodyText = _sourceManager.GetText(sourceRef, createEventStatement.Body.Meta),
        };
    }
}
