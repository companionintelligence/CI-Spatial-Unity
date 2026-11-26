// The rules the Runtime assembly is held to, checked rather than promised: it references no Unity assembly, keeps no
// static mutable state, and reads no clock and no random numbers (so the same input gives the same output).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Companion.Spatial.Tests
{
    public class RuntimeRulesTests
    {
        private static readonly Assembly Runtime = typeof(Vec3).Assembly;

        private static string[] RuntimeSources()
        {
            return Directory.GetFiles(Path.Combine(TestPaths.PackageRoot, "Runtime"), "*.cs", SearchOption.AllDirectories);
        }

        // The lines of code of a source file: comments (and so the XML documentation) are left out.
        private static string[] CodeLines(string path)
        {
            return File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)).ToArray();
        }

        [Test]
        public void TheRuntimeAssembly_ReferencesNoUnityAssembly()
        {
            var references = Runtime.GetReferencedAssemblies().Select(name => name.Name).ToArray();

            Assert.AreEqual("Companion.Spatial", Runtime.GetName().Name);
            CollectionAssert.IsEmpty(references.Where(name => name.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                                                              name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                                                              name.StartsWith("Unity.", StringComparison.Ordinal)),
                "references: " + string.Join(", ", references));
        }

        [Test]
        public void NoSourceFileOfTheRuntime_UsesUnity()
        {
            var sources = RuntimeSources();
            Assert.Greater(sources.Length, 20, "the sources of the Runtime folder were found");

            foreach (var path in sources)
            {
                var offending = CodeLines(path).Where(line => line.Contains("using UnityEngine") || line.Contains("using UnityEditor")).ToArray();
                Assert.IsEmpty(offending, Path.GetFileName(path));
            }
        }

        [Test]
        public void EveryStaticFieldOfTheRuntime_IsReadOnlyOrConstant()
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var mutable = new System.Collections.Generic.List<string>();
            foreach (var type in Runtime.GetTypes())
            {
                if (type.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                {
                    continue; // the closure and enumerator classes the compiler makes (they cache delegates)
                }

                foreach (var field in type.GetFields(flags))
                {
                    if (!field.IsLiteral && !field.IsInitOnly && field.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                    {
                        mutable.Add(type.FullName + "." + field.Name);
                    }
                }
            }

            CollectionAssert.IsEmpty(mutable, "static fields that can change: " + string.Join(", ", mutable));
        }

        [Test]
        public void NoStaticPropertyOfTheRuntime_HasASetter()
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var settable = Runtime.GetTypes()
                .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                .SelectMany(type => type.GetProperties(flags).Where(property => property.GetSetMethod(true) != null).Select(property => type.FullName + "." + property.Name))
                .ToArray();

            CollectionAssert.IsEmpty(settable, "static properties with a setter: " + string.Join(", ", settable));
        }

        [Test]
        public void NoSourceFileOfTheRuntime_ReadsAClockOrARandomNumber()
        {
            string[] forbidden =
            {
                "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow", "DateTime.Today",
                "Stopwatch", "Environment.TickCount", "new Random(", "Random.Shared", "Guid.NewGuid", "RandomNumberGenerator"
            };

            foreach (var path in RuntimeSources())
            {
                foreach (var line in CodeLines(path))
                {
                    foreach (var word in forbidden)
                    {
                        Assert.IsFalse(line.Contains(word), Path.GetFileName(path) + " uses " + word + ": " + line.Trim());
                    }
                }
            }
        }

        [Test]
        public void EveryPublicTypeOfTheRuntime_IsInACompanionSpatialNamespace()
        {
            var strays = Runtime.GetExportedTypes()
                .Where(type => type.Namespace != "Companion.Spatial" && (type.Namespace == null || !type.Namespace.StartsWith("Companion.Spatial.", StringComparison.Ordinal)))
                .Select(type => type.FullName)
                .ToArray();

            CollectionAssert.IsEmpty(strays);
        }
    }
}
