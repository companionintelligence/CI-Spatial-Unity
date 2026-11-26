// Tests for the structural checker the schema tests rely on: it must reject what a schema forbids (or the schema
// tests prove nothing), and it must refuse a schema that uses a keyword it does not check.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class MiniSchemaTests
    {
        private static int Errors(string schema, string instance)
        {
            return MiniSchema.Validate(JsonAssert.Parse(instance), JsonAssert.ParseObject(schema)).Count;
        }

        [Test]
        public void Required_NamesTheMissingKey()
        {
            var errors = MiniSchema.Validate(JsonAssert.Parse("{ 'b': 1 }"), JsonAssert.ParseObject("{ 'type': 'object', 'required': [ 'a', 'b' ] }"));

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("\"a\"", errors[0]);
        }

        [TestCase("{ 'type': 'integer' }", "2", 0)]
        [TestCase("{ 'type': 'integer' }", "2.0", 0)]
        [TestCase("{ 'type': 'integer' }", "2.5", 1)]
        [TestCase("{ 'type': 'integer' }", "'2'", 1)]
        [TestCase("{ 'type': 'number' }", "2", 0)]
        [TestCase("{ 'type': 'number' }", "2.5", 0)]
        [TestCase("{ 'type': 'number' }", "'2'", 1)]
        [TestCase("{ 'type': 'string' }", "'a'", 0)]
        [TestCase("{ 'type': 'string' }", "1", 1)]
        [TestCase("{ 'type': 'array' }", "[]", 0)]
        [TestCase("{ 'type': 'array' }", "{}", 1)]
        [TestCase("{ 'type': 'object' }", "{}", 0)]
        [TestCase("{ 'type': 'object' }", "[]", 1)]
        [TestCase("{ 'type': 'boolean' }", "true", 0)]
        [TestCase("{ 'type': 'boolean' }", "1", 1)]
        [TestCase("{ 'type': 'null' }", "null", 0)]
        [TestCase("{ 'type': [ 'string', 'null' ] }", "null", 0)]
        [TestCase("{ 'type': [ 'string', 'null' ] }", "5", 1)]
        public void Type_IsChecked(string schema, string instance, int expectedErrors)
        {
            Assert.AreEqual(expectedErrors, Errors(schema, instance));
        }

        [TestCase("{ 'const': 2 }", "2", 0)]
        [TestCase("{ 'const': 2 }", "2.0", 0)]
        [TestCase("{ 'const': 2 }", "3", 1)]
        [TestCase("{ 'const': 'meters' }", "'meters'", 0)]
        [TestCase("{ 'const': 'meters' }", "'feet'", 1)]
        [TestCase("{ 'enum': [ 'cv', 'device' ] }", "'cv'", 0)]
        [TestCase("{ 'enum': [ 'cv', 'device' ] }", "'robot'", 1)]
        public void ConstAndEnum_AreChecked(string schema, string instance, int expectedErrors)
        {
            Assert.AreEqual(expectedErrors, Errors(schema, instance));
        }

        [TestCase("{ 'minimum': 0, 'maximum': 1 }", "0", 0)]
        [TestCase("{ 'minimum': 0, 'maximum': 1 }", "1", 0)]
        [TestCase("{ 'minimum': 0, 'maximum': 1 }", "-0.1", 1)]
        [TestCase("{ 'minimum': 0, 'maximum': 1 }", "1.1", 1)]
        [TestCase("{ 'exclusiveMinimum': 0 }", "0", 1)]
        [TestCase("{ 'exclusiveMinimum': 0 }", "0.001", 0)]
        [TestCase("{ 'exclusiveMaximum': 1 }", "1", 1)]
        [TestCase("{ 'minLength': 1 }", "''", 1)]
        [TestCase("{ 'minLength': 1 }", "'a'", 0)]
        [TestCase("{ 'maxLength': 2 }", "'abc'", 1)]
        [TestCase("{ 'minItems': 2 }", "[ 1 ]", 1)]
        [TestCase("{ 'maxItems': 2 }", "[ 1, 2, 3 ]", 1)]
        [TestCase("{ 'minItems': 4, 'maxItems': 4 }", "[ 1, 2, 3, 4 ]", 0)]
        public void Limits_AreChecked(string schema, string instance, int expectedErrors)
        {
            Assert.AreEqual(expectedErrors, Errors(schema, instance));
        }

        [TestCase("2026-11-25T10:00:00Z", 0)]
        [TestCase("2026-11-25T10:00:00.1234567Z", 0)]
        [TestCase("2026-11-25T10:00:00+02:00", 0)]
        [TestCase("2026-11-25", 1)]
        [TestCase("yesterday", 1)]
        [TestCase("", 1)]
        public void DateTimeFormat_IsChecked(string text, int expectedErrors)
        {
            var errors = MiniSchema.Validate(new JValue(text), JsonAssert.ParseObject("{ 'type': 'string', 'format': 'date-time' }"));

            Assert.AreEqual(expectedErrors, errors.Count);
        }

        [Test]
        public void Items_AreCheckedWithTheirPath()
        {
            var errors = MiniSchema.Validate(JsonAssert.Parse("{ 'list': [ 1, 'x', 3 ] }"),
                JsonAssert.ParseObject("{ 'properties': { 'list': { 'type': 'array', 'items': { 'type': 'number' } } } }"));

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("$.list[1]", errors[0]);
        }

        [Test]
        public void AdditionalProperties_FalseForbidsOtherKeys_TrueAllowsThem_ASchemaChecksThem()
        {
            Assert.AreEqual(1, Errors("{ 'properties': { 'a': {} }, 'additionalProperties': false }", "{ 'a': 1, 'b': 2 }"));
            Assert.AreEqual(0, Errors("{ 'properties': { 'a': {} }, 'additionalProperties': true }", "{ 'a': 1, 'b': 2 }"));
            Assert.AreEqual(0, Errors("{ 'properties': { 'a': {} } }", "{ 'a': 1, 'b': 2 }"));
            Assert.AreEqual(1, Errors("{ 'additionalProperties': { 'type': 'number' } }", "{ 'a': 1, 'b': 'x' }"));
        }

        [Test]
        public void Ref_FollowsAPointerInsideTheDocument()
        {
            const string schema = "{ 'properties': { 'p': { '$ref': '#/$defs/point' } }, '$defs': { 'point': { 'type': 'object', 'required': [ 'x' ] } } }";

            Assert.AreEqual(0, Errors(schema, "{ 'p': { 'x': 1 } }"));
            Assert.AreEqual(1, Errors(schema, "{ 'p': { 'y': 1 } }"));
            Assert.AreEqual(1, Errors(schema, "{ 'p': 5 }"));
            Assert.Throws<InvalidOperationException>(() => Errors("{ '$ref': '#/$defs/missing' }", "1"));
            Assert.Throws<InvalidOperationException>(() => Errors("{ '$ref': 'http://example.test/other.json' }", "1"));
        }

        [Test]
        public void UndeclaredKeys_ListsTheKeysTheSchemaNeverMentions()
        {
            var schema = JsonAssert.ParseObject(@"{
                'properties': { 'a': { 'properties': { 'x': {} } }, 'list': { 'items': { '$ref': '#/$defs/item' } } },
                '$defs': { 'item': { 'properties': { 'id': {} } } }
            }");
            var instance = JsonAssert.Parse("{ 'a': { 'x': 1, 'y': 2 }, 'b': 3, 'list': [ { 'id': 1 }, { 'id': 2, 'extra': 3 } ] }");

            CollectionAssert.AreEquivalent(new[] { "$.a.y", "$.b", "$.list[1].extra" }, MiniSchema.UndeclaredKeys(instance, schema).ToArray());
        }

        [Test]
        public void UnsupportedKeywords_AreListedWithTheirPath()
        {
            var schema = JsonAssert.Parse(@"{
                '$schema': 'x', 'title': 't', 'type': 'object', 'oneOf': [],
                'properties': { 'a': { 'type': 'string', 'pattern': '^a' }, 'b': { 'items': { 'allOf': [] } } },
                '$defs': { 'c': { 'if': {} } }
            }");

            CollectionAssert.AreEquivalent(new[] { "$.oneOf", "$.properties.a.pattern", "$.properties.b.items.allOf", "$.$defs.c.if" },
                MiniSchema.UnsupportedKeywords(schema).ToArray());
        }
    }
}
