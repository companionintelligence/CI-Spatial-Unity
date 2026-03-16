using CompanionScan.UI;
using NUnit.Framework;
using UnityEngine;

namespace CompanionScan.Tests.PlayMode
{
    public class ControllerMenuUiHealthTests
    {
        [Test]
        public void HealthStatus_ReportsMissingDependencies()
        {
            var menuGo = new GameObject("MenuUI");
            var menu = menuGo.AddComponent<ControllerMenuUI>();

            var health = menu.GetUiHealthStatus();

            Assert.IsFalse(health.HasPipeline);
            Assert.IsFalse(health.HasHudLabels);
            Assert.Greater(health.MissingHudLabels, 0);

            Object.DestroyImmediate(menuGo);
        }
    }
}