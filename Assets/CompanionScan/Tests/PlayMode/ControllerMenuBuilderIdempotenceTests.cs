using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class ControllerMenuBuilderIdempotenceTests
    {
        [Test]
        public void Start_CalledTwice_DoesNotDuplicateIndicators()
        {
            var right = new GameObject("RightControllerAnchor");
            var left = new GameObject("LeftControllerAnchor");
            var host = new GameObject("BuilderHost");
            var builder = host.AddComponent<ControllerMenuBuilder>();

            builder.SendMessage("Start");
            builder.SendMessage("Start");

            Assert.AreEqual(3, CountNamedChildren(right.transform, "A_Circle", "B_Circle", "RTrigger_Circle"));
            Assert.AreEqual(3, CountNamedChildren(left.transform, "X_Circle", "Y_Circle", "LTrigger_Circle"));

            Object.DestroyImmediate(host);
            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
        }

        private static int CountNamedChildren(Transform parent, params string[] names)
        {
            int count = 0;
            foreach (var n in names)
            {
                if (parent.Find(n) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}