using System.Reflection;
using CompanionScan.Mapping;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class SpatialCapturePipelineClientInjectionTests
    {
        [Test]
        public void SetInferenceClient_OverridesRuntimeClient()
        {
            var go = new GameObject("Pipeline");
            var pipeline = go.AddComponent<SpatialCapturePipeline>();
            var fake = new FakeCvInferenceClient();

            pipeline.SetInferenceClient(fake);

            var field = typeof(SpatialCapturePipeline).GetField("_inferenceClient", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.AreSame(fake, field.GetValue(pipeline));

            Object.DestroyImmediate(go);
        }
    }
}