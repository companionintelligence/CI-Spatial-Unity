using NUnit.Framework;
using UnityEngine;

namespace Unity.CI.MRTemplate.Tests
{
    /// <summary>
    /// Edit mode tests for MRTemplate components.
    /// These tests run in the Unity Editor without entering Play mode.
    /// </summary>
    public class MRTemplateComponentTests
    {
        GameObject m_TestGameObject;

        [SetUp]
        public void Setup()
        {
            m_TestGameObject = new GameObject("TestObject");
        }

        [TearDown]
        public void Teardown()
        {
            if (m_TestGameObject != null)
                Object.DestroyImmediate(m_TestGameObject);
        }

        [Test]
        public void PositionAtStart_ComponentCanBeAdded()
        {
            var component = m_TestGameObject.AddComponent<PositionAtStart>();
            Assert.IsNotNull(component, "PositionAtStart component should be added successfully");
        }

        [Test]
        public void PinchVizController_ComponentCanBeAdded()
        {
            var component = m_TestGameObject.AddComponent<PinchVizController>();
            Assert.IsNotNull(component, "PinchVizController component should be added successfully");
        }

        [Test]
        public void FadeMaterial_ComponentCanBeAdded()
        {
            var component = m_TestGameObject.AddComponent<FadeMaterial>();
            Assert.IsNotNull(component, "FadeMaterial component should be added successfully");
        }

        [Test]
        public void FadePlaneMaterial_ComponentCanBeAdded()
        {
            var component = m_TestGameObject.AddComponent<FadePlaneMaterial>();
            Assert.IsNotNull(component, "FadePlaneMaterial component should be added successfully");
        }

        [Test]
        public void SpawnedObjectsManager_RequiresObjectSpawner()
        {
            // This component requires ObjectSpawner, so adding it should automatically add ObjectSpawner
            var manager = m_TestGameObject.AddComponent<SpawnedObjectsManager>();
            var spawner = m_TestGameObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ObjectSpawner>();
            
            Assert.IsNotNull(manager, "SpawnedObjectsManager should be added");
            Assert.IsNotNull(spawner, "ObjectSpawner should be automatically added due to RequireComponent");
        }
    }
}
