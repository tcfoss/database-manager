using System.Collections;
using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO.JsonConverters;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.Common.IdentifierTests.IdentifierSerializer), typeof(Identifier), typeof(CatalogIdentifier), typeof(SchemaIdentifier), typeof(ObjectIdentifier))]
[assembly: RegisterXunitSerializer(typeof(TcfOss.DatabaseManager.Core.Tests.Common.IdentifierTests.ObjectNameSerializer), typeof(ObjectName))]
namespace TcfOss.DatabaseManager.Core.Tests.Common;

public partial class IdentifierTests
{
    private static readonly CatalogIdentifier s_catalog = new("def", QuoteStyle.Ansi);
    private static readonly CatalogIdentifier s_catalogAnsi = new("def", QuoteStyle.Ansi);
    private static readonly SchemaIdentifier s_schema = new("schema", s_catalog, QuoteStyle.Brackets);
    private static readonly SchemaIdentifier s_schemaAnsi = new("schema", s_catalogAnsi, QuoteStyle.Ansi);
    private static readonly ObjectIdentifier s_object = new("table", s_schema, QuoteStyle.Backticks);
    private static readonly ObjectIdentifier s_objectAnsi = new("table", s_schemaAnsi, QuoteStyle.Ansi);

    [Theory]
    [ClassData(typeof(ToSimpleIdentifierTestData))]
    public void To_Simple_Identifier(Identifier full, Identifier simple, QuoteStyle? quoteStyle)
    {
        // Act
        var result = full.ToSimpleIdentifier(quoteStyle);

        // Assert
        Assert.Equal(simple, result);
    }

    public class ToSimpleIdentifierTestData : IEnumerable<TheoryDataRow<Identifier, Identifier, QuoteStyle?>>
    {

        public IEnumerator<TheoryDataRow<Identifier, Identifier, QuoteStyle?>> GetEnumerator()
        {
            foreach (var data in GetTestData())
            {
                yield return data;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static IEnumerable<TheoryDataRow<Identifier, Identifier, QuoteStyle?>> GetTestData()
        {
            /* I. No quoteStyle specified: original QuoteStyle is preserved */
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object, QuoteStyle.Brackets), new Identifier("column", QuoteStyle.Brackets), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object, QuoteStyle.Ansi), new Identifier("column", QuoteStyle.Ansi), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object, QuoteStyle.Backticks), new Identifier("column", QuoteStyle.Backticks), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object), new Identifier("column"), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema, QuoteStyle.Brackets), new Identifier("table", QuoteStyle.Brackets), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema, QuoteStyle.Ansi), new Identifier("table", QuoteStyle.Ansi), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema, QuoteStyle.Backticks), new Identifier("table", QuoteStyle.Backticks), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema), new Identifier("table"), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new SchemaIdentifier("schema", s_catalog, QuoteStyle.Brackets), new Identifier("schema", QuoteStyle.Brackets), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new SchemaIdentifier("schema", s_catalog, QuoteStyle.Ansi), new Identifier("schema", QuoteStyle.Ansi), null);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(s_catalog, new Identifier("def", QuoteStyle.Ansi), null);
            /* II. QuoteStyle specified: QuoteStyle is applied */
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object, QuoteStyle.Brackets), new Identifier("column", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object, QuoteStyle.Ansi), new Identifier("column", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object, QuoteStyle.Backticks), new Identifier("column", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ColumnIdentifier("column", s_object), new Identifier("column", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema, QuoteStyle.Brackets), new Identifier("table", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema, QuoteStyle.Ansi), new Identifier("table", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema, QuoteStyle.Backticks), new Identifier("table", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new ObjectIdentifier("table", s_schema), new Identifier("table", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new SchemaIdentifier("schema", s_catalog, QuoteStyle.Brackets), new Identifier("schema", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(new SchemaIdentifier("schema", s_catalog, QuoteStyle.Ansi), new Identifier("schema", QuoteStyle.Backticks), QuoteStyle.Backticks);
            yield return new TheoryDataRow<Identifier, Identifier, QuoteStyle?>(s_catalog, new Identifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks);
        }
    }

    [Theory]
    [ClassData(typeof(FromObjectNameTestData))]
    public void From_Object_Name(ObjectName objectName, SchemaIdentifier? expectedSchema, ObjectIdentifier? expectedObject, ColumnIdentifier? expectedColumn, QuoteStyle? quoteStyle)
    {
        if (expectedSchema != null)
        {
            // Test SchemaIdentifier
            var schema = SchemaIdentifier.FromObjectName(objectName, s_catalogAnsi, quoteStyle);
            Assert.Equal(expectedSchema, schema);
            Assert.Equal(expectedSchema.ToString(), schema.ToString());
        }
        else
        {
            Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => SchemaIdentifier.FromObjectName(objectName, s_catalogAnsi, quoteStyle));
        }

        if (expectedObject != null)
        {
            // Test ObjectIdentifier
            var objectId = ObjectIdentifier.FromObjectName(objectName, s_schemaAnsi, quoteStyle);
            Assert.Equal(expectedObject, objectId);
            Assert.Equal(expectedObject.ToString(), objectId.ToString());
        }
        else
        {
            Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => ObjectIdentifier.FromObjectName(objectName, s_schemaAnsi, quoteStyle));
        }

        if (expectedColumn != null)
        {
            // Test ColumnIdentifier
            var columnId = ColumnIdentifier.FromObjectName(objectName, s_objectAnsi, quoteStyle);
            Assert.Equal(expectedColumn, columnId);
            Assert.Equal(expectedColumn.ToString(), columnId.ToString());
        }
        else
        {
            Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => ColumnIdentifier.FromObjectName(objectName, s_objectAnsi, quoteStyle));
        }
    }

    public class FromObjectNameTestData : IEnumerable<TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>>
    {
        public IEnumerator<TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>> GetEnumerator()
        {
            /* I. QuoteStyle is overwritten. */
            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks), new Identifier("part2", QuoteStyle.Backticks), new Identifier("part3", QuoteStyle.Backticks), new Identifier("part4", QuoteStyle.Backticks)]),
                null,
                null,
                new ColumnIdentifier("part4", new ObjectIdentifier("part3", new SchemaIdentifier("part2", new CatalogIdentifier("part1", QuoteStyle.Ansi), QuoteStyle.Ansi), QuoteStyle.Ansi), QuoteStyle.Ansi),
                QuoteStyle.Ansi);
            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks), new Identifier("part2", QuoteStyle.Backticks), new Identifier("part3", QuoteStyle.Backticks)]),
                null,
                new ObjectIdentifier("part3", new SchemaIdentifier("part2", new CatalogIdentifier("part1", QuoteStyle.Ansi), QuoteStyle.Ansi), QuoteStyle.Ansi),
                new ColumnIdentifier("part3", new ObjectIdentifier("part2", new SchemaIdentifier("part1", s_catalogAnsi, QuoteStyle.Ansi), QuoteStyle.Ansi), QuoteStyle.Ansi),
                QuoteStyle.Ansi);

            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks), new Identifier("part2", QuoteStyle.Backticks)]),
                new SchemaIdentifier("part2", new CatalogIdentifier("part1", QuoteStyle.Ansi), QuoteStyle.Ansi),
                new ObjectIdentifier("part2", new SchemaIdentifier("part1", s_catalogAnsi, QuoteStyle.Ansi), QuoteStyle.Ansi),
                new ColumnIdentifier("part2", new ObjectIdentifier("part1", s_schemaAnsi, QuoteStyle.Ansi), QuoteStyle.Ansi),
                QuoteStyle.Ansi);

            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks)]),
                new SchemaIdentifier("part1", s_catalogAnsi, QuoteStyle.Ansi),
                new ObjectIdentifier("part1", s_schemaAnsi, QuoteStyle.Ansi),
                new ColumnIdentifier("part1", s_objectAnsi, QuoteStyle.Ansi),
                QuoteStyle.Ansi);

            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([]),
                null,
                null,
                null,
                QuoteStyle.Ansi);
            /* II. QuoteStyle is not specified, original QuoteStyle is preserved. */
            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks), new Identifier("part2", QuoteStyle.Backticks), new Identifier("part3", QuoteStyle.Backticks), new Identifier("part4", QuoteStyle.Backticks)]),
                null,
                null,
                new ColumnIdentifier("part4", new ObjectIdentifier("part3", new SchemaIdentifier("part2", new CatalogIdentifier("part1", QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                null);
            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks), new Identifier("part2", QuoteStyle.Backticks), new Identifier("part3", QuoteStyle.Backticks)]),
                null,
                new ObjectIdentifier("part3", new SchemaIdentifier("part2", new CatalogIdentifier("part1", QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ColumnIdentifier("part3", new ObjectIdentifier("part2", new SchemaIdentifier("part1", s_catalogAnsi, QuoteStyle.Backticks), QuoteStyle.Backticks), QuoteStyle.Backticks),
                null);
            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks), new Identifier("part2", QuoteStyle.Backticks)]),
                new SchemaIdentifier("part2", new CatalogIdentifier("part1", QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ObjectIdentifier("part2", new SchemaIdentifier("part1", s_catalogAnsi, QuoteStyle.Backticks), QuoteStyle.Backticks),
                new ColumnIdentifier("part2", new ObjectIdentifier("part1", s_schemaAnsi, QuoteStyle.Backticks), QuoteStyle.Backticks),
                null);
            yield return new TheoryDataRow<ObjectName, SchemaIdentifier?, ObjectIdentifier?, ColumnIdentifier?, QuoteStyle?>(
                new ObjectName([new Identifier("part1", QuoteStyle.Backticks)]),
                new SchemaIdentifier("part1", s_catalogAnsi, QuoteStyle.Backticks),
                new ObjectIdentifier("part1", s_schemaAnsi, QuoteStyle.Backticks),
                new ColumnIdentifier("part1", s_objectAnsi, QuoteStyle.Backticks),
                null);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Theory]
    [ClassData(typeof(ToObjectNameTestData))]
    public void To_Object_Name(ObjectIdentifier objectIdentifier, ObjectName expectedObjectName, int depth)
    {
        // Act
        var result = objectIdentifier.ToObjectName(depth);

        // Assert
        Assert.Equal(expectedObjectName, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void To_Object_Name_Invalid_Depth_Throws(int depth)
    {
        // Act & Assert
        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => s_object.ToObjectName(depth));
        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => s_objectAnsi.ToObjectName(depth));
    }

    public class ToObjectNameTestData : IEnumerable<TheoryDataRow<ObjectIdentifier, ObjectName, int>>
    {
        public IEnumerator<TheoryDataRow<ObjectIdentifier, ObjectName, int>> GetEnumerator()
        {
            yield return new TheoryDataRow<ObjectIdentifier, ObjectName, int>(
                s_object,
                new ObjectName([new Identifier("def", QuoteStyle.Ansi), new Identifier("schema", QuoteStyle.Brackets), new Identifier("table", QuoteStyle.Backticks)]),
                3);
            yield return new TheoryDataRow<ObjectIdentifier, ObjectName, int>(
                s_object,
                new ObjectName([new Identifier("schema", QuoteStyle.Brackets), new Identifier("table", QuoteStyle.Backticks)]),
                2);
            yield return new TheoryDataRow<ObjectIdentifier, ObjectName, int>(
                s_object,
                new ObjectName([new Identifier("table", QuoteStyle.Backticks)]),
                1);
            yield return new TheoryDataRow<ObjectIdentifier, ObjectName, int>(
                s_objectAnsi,
                new ObjectName([new Identifier("def", QuoteStyle.Ansi), new Identifier("schema", QuoteStyle.Ansi), new Identifier("table", QuoteStyle.Ansi)]),
                3);
            yield return new TheoryDataRow<ObjectIdentifier, ObjectName, int>(
                s_objectAnsi,
                new ObjectName([new Identifier("schema", QuoteStyle.Ansi), new Identifier("table", QuoteStyle.Ansi)]),
                2);
            yield return new TheoryDataRow<ObjectIdentifier, ObjectName, int>(
                s_objectAnsi,
                new ObjectName([new Identifier("table", QuoteStyle.Ansi)]),
                1);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Theory]
    [ClassData(typeof(ToSimpleStringTestData))]
    public void ToSimpleString(ObjectIdentifier objectIdentifier)
    {
        // Act
        var result = objectIdentifier.ToSimpleString();

        // Assert
        Assert.Equal("def.schema.table", result);
    }

    public class ToSimpleStringTestData : IEnumerable<TheoryDataRow<ObjectIdentifier>>
    {
        public IEnumerator<TheoryDataRow<ObjectIdentifier>> GetEnumerator()
        {
            yield return new TheoryDataRow<ObjectIdentifier>(s_object);
            yield return new TheoryDataRow<ObjectIdentifier>(s_objectAnsi);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Theory]
    [InlineData(QuoteStyle.Ansi, ExtendedQuoteStyle.Ansi, ExtendedQuoteStyle.Ansi)]
    [InlineData(QuoteStyle.Backticks, ExtendedQuoteStyle.Ansi, ExtendedQuoteStyle.Backticks)]
    [InlineData(QuoteStyle.Brackets, ExtendedQuoteStyle.Ansi, ExtendedQuoteStyle.Brackets)]
    [InlineData(QuoteStyle.None, ExtendedQuoteStyle.Ansi, ExtendedQuoteStyle.None)]
    [InlineData(QuoteStyle.Ansi, ExtendedQuoteStyle.Backticks, ExtendedQuoteStyle.Ansi)]
    [InlineData(QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, ExtendedQuoteStyle.Backticks)]
    [InlineData(QuoteStyle.Brackets, ExtendedQuoteStyle.Backticks, ExtendedQuoteStyle.Brackets)]
    [InlineData(QuoteStyle.None, ExtendedQuoteStyle.Backticks, ExtendedQuoteStyle.None)]
    [InlineData(QuoteStyle.Ansi, ExtendedQuoteStyle.Brackets, ExtendedQuoteStyle.Ansi)]
    [InlineData(QuoteStyle.Backticks, ExtendedQuoteStyle.Brackets, ExtendedQuoteStyle.Backticks)]
    [InlineData(QuoteStyle.Brackets, ExtendedQuoteStyle.Brackets, ExtendedQuoteStyle.Brackets)]
    [InlineData(QuoteStyle.None, ExtendedQuoteStyle.Brackets, ExtendedQuoteStyle.None)]
    [InlineData(QuoteStyle.Ansi, ExtendedQuoteStyle.None, ExtendedQuoteStyle.Ansi)]
    [InlineData(QuoteStyle.Backticks, ExtendedQuoteStyle.None, ExtendedQuoteStyle.Backticks)]
    [InlineData(QuoteStyle.Brackets, ExtendedQuoteStyle.None, ExtendedQuoteStyle.Brackets)]
    [InlineData(QuoteStyle.None, ExtendedQuoteStyle.None, ExtendedQuoteStyle.None)]
    [InlineData(QuoteStyle.Ansi, ExtendedQuoteStyle.SingleQuote, ExtendedQuoteStyle.Ansi)]
    [InlineData(QuoteStyle.Backticks, ExtendedQuoteStyle.SingleQuote, ExtendedQuoteStyle.Backticks)]
    [InlineData(QuoteStyle.Brackets, ExtendedQuoteStyle.SingleQuote, ExtendedQuoteStyle.Brackets)]
    [InlineData(QuoteStyle.None, ExtendedQuoteStyle.SingleQuote, ExtendedQuoteStyle.None)]
    public void ExtendedIdentifier_WithQuoteStyle(QuoteStyle quoteStyle, ExtendedQuoteStyle initialQuoteStyle, ExtendedQuoteStyle expectedQuoteStyle)
    {
        var actual = new ExtendedIdentifier("name", initialQuoteStyle).WithQuoteStyle(quoteStyle);
        var expected = new ExtendedIdentifier("name", expectedQuoteStyle);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(new[] { "mycat", "myschema", "mytable" }, QuoteStyle.Brackets, "[mycat].[myschema].[mytable]")]
    [InlineData(new[] { "myschema", "mytable" }, QuoteStyle.Brackets, "[def].[myschema].[mytable]")]
    [InlineData(new[] { "mytable" }, QuoteStyle.Brackets, "[def].[schema].[mytable]")]
    public void ObjectIdentifier_FromStrings(IReadOnlyList<string> parts, QuoteStyle quoteStyle, string expected)
    {
        var objectName = ObjectIdentifier.FromStrings(parts, s_schema, quoteStyle);
        Assert.Equal(expected, objectName.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void ObjectIdentifier_FromStrings_InvalidLengthThrows(int length)
    {
        var parts = Enumerable.Repeat("part", length).ToArray();
        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => ObjectIdentifier.FromStrings(parts, s_schema, QuoteStyle.Brackets));
    }

    public class IdentifierSerializer : IXunitSerializer
    {
        public string Serialize(object value)
        {
            var serialized = value.ToString();
            if (serialized != null)
            {
                return serialized;
            }
            throw new ArgumentException("Value must be of type Identifier", nameof(value));
        }

        public object Deserialize(Type type, string value)
        {
            return type switch
            {
                not null when type == typeof(CatalogIdentifier) => CatalogIdentifier.FromIdentifier(IdentifierConverter.Parse(value), null),
                not null when type == typeof(SchemaIdentifier) => SchemaIdentifierConverter.Parse(value),
                not null when type == typeof(ObjectIdentifier) => ObjectIdentifierConverter.Parse(value),
                not null when type == typeof(ColumnIdentifier) => ColumnIdentifierConverter.Parse(value),
                not null when type == typeof(Identifier) => IdentifierConverter.Parse(value),
                _ => throw new ArgumentException($"Cannot deserialize to type {type!.Name}", nameof(type))
            };
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (value is Identifier)
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = $"Type {type.Name} is not serializable by IdentifierSerializer.";
            return false;
        }
    }

    public partial class ObjectNameSerializer : IXunitSerializer
    {
        private static Regex IdentifierRegex { get; } = GenerateNameComponentRegex();

        public string Serialize(object value)
        {
            if (value is ObjectName objectName)
            {
                return objectName.ToString();
            }
            throw new ArgumentException("Value must be of type ObjectName", nameof(value));
        }

        public object Deserialize(Type type, string value)
        {
            if (type == typeof(ObjectName))
            {
                return Parse(value);
            }
            throw new ArgumentException($"Cannot deserialize to type {type.Name}", nameof(type));
        }

        public bool IsSerializable(Type type, object? value, out string failureReason)
        {
            if (value is ObjectName)
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = $"Type {type.Name} is not serializable by ObjectNameSerializer.";
            return false;
        }

        private static ObjectName Parse(string given)
        {
            var parts = new SqlValueList<Identifier>();

            var pos = 0;
            while (true)
            {
                if (pos >= given.Length)
                {
                    break;
                }
                if (given[pos] == '.')
                {
                    pos++;
                }
                var match = IdentifierRegex.Match(given, pos);
                if (!match.Success)
                {
                    throw new ArgumentException($"Invalid ObjectName format: '{given}'. Expected format is '[[[[part1.]part2.]part3.]part4]...'.");
                }
                var part = match.Groups[1].Value;
                parts.Add(ParseSingle(part));
                pos = match.Index + match.Length;
            }
            return new ObjectName(parts);
        }

        private static Identifier ParseSingle(string given)
        {
            var quoteStyle = given[0] switch
            {
                '`' => QuoteStyle.Backticks,
                '[' => QuoteStyle.Brackets,
                '"' => QuoteStyle.Ansi,
                _ => QuoteStyle.None,
            };
            if (quoteStyle != QuoteStyle.None)
            {
                given = given[1..^1]; // Remove the surrounding quotes
            }

            return new Identifier(given, quoteStyle);
        }

        [GeneratedRegex(@"\G((?:`[^`]+`)|(?:\[[^\[\]]+\])|(?:""[^""]+"")|(?:[A-Za-z_]\w+))")]
        private static partial Regex GenerateNameComponentRegex();
    }
}
