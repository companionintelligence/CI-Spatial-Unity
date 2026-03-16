using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class XRControllerInputDebounceTests
    {
        [Test]
        public void CanDispatch_RespectsCooldown()
        {
            var go = new GameObject("XRControllerInput");
            var input = go.AddComponent<XRControllerInput>();

            Assert.IsTrue(input.CanDispatch("capture", 1.0f));
            Assert.IsFalse(input.CanDispatch("capture", 1.05f));
            Assert.IsTrue(input.CanDispatch("capture", 1.2f));

            Object.DestroyImmediate(go);
        }
    }
}