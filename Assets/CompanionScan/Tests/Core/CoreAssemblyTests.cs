using System;
using System.IO;
using System.Linq;
using CompanionScan.Core;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Core.Tests
{
    /// <summary>
    /// The platform-neutral assembly stays free of the Meta SDK. This test assembly references only
    /// <c>CompanionScan.Core</c>, so a backend (phone, Aura, PICO, a recorded session) can be built and
    /// tested without Oculus.* on the machine's compile path.
    /// </summary>
    public class CoreAssemblyTests
    {
        private static readonly string[] MetaAssemblyPrefixes =
        {
            "Oculus", "Meta.", "Unity.XR.Oculus", "Unity.XR.MetaOpenXR", "Unity.XR.Meta"
        };

        [Test]
        public void Core_IsTheAssemblyNamedCompanionScanCore()
        {
            Assert.AreEqual("CompanionScan.Core", typeof(CameraFrame).Assembly.GetName().Name);
        }

        [Test]
        public void Core_ReferencesNoMetaAssembly()
        {
            var references = typeof(CameraFrame).Assembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .Where(name => MetaAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                .ToList();

            Assert.IsEmpty(references, "CompanionScan.Core references the Meta SDK: " + string.Join(", ", references));
        }

        [Test]
        public void CoreAssemblyDefinition_ListsNoMetaReference()
        {
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "CompanionScan", "CompanionScan.Core.asmdef"));

            StringAssert.DoesNotContain("Oculus", json);
            StringAssert.DoesNotContain("Meta.", json);
            StringAssert.DoesNotContain("MetaXR", json);
        }

        // The check above only means something if it can see a Meta dependency when there is one.
        [Test]
        public void QuestAssembly_ReferencesCoreAndTheMetaSdk_SoTheCheckCanSeeADependency()
        {
            var quest = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "CompanionScan.Quest");
            Assert.IsNotNull(quest, "the Quest assembly is not loaded");

            var names = quest.GetReferencedAssemblies().Select(name => name.Name).ToList();

            CollectionAssert.Contains(names, "CompanionScan.Core");
            Assert.IsTrue(names.Any(name => name.StartsWith("Oculus", StringComparison.Ordinal)),
                "the Quest assembly does not reference Oculus.*: " + string.Join(", ", names));
        }
    }
}
