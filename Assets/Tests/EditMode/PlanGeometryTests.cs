using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DrillingAssistant.Tests
{
    public class PlanGeometryTests
    {
        // The revealer's default: a structure is found within 25 cm.
        private const float RevealDistanceSq = 0.25f * 0.25f;

        private static ScannedStructure CableRun(params Vector3[] path)
        {
            return new ScannedStructure
            {
                Type = StructureType.CableRun,
                LocalPosition = path[0],
                Path = new List<Vector3>(path)
            };
        }

        [Test]
        public void APointOnACableRunIsWithinRevealDistance()
        {
            // Down the wall, then a right-hand elbow: the probe sits on the elbow leg.
            var cable = CableRun(new Vector3(0f, 1f, 0f), new Vector3(0f, -1f, 0f), new Vector3(1f, -1f, 0f));

            Assert.Less(PlanGeometry.DistanceSq(new Vector3(0.5f, -1f, 0f), cable), RevealDistanceSq);
        }

        [Test]
        public void APointAMetreOffACableRunIsNot()
        {
            var cable = CableRun(new Vector3(0f, 1f, 0f), new Vector3(0f, -1f, 0f));

            Assert.AreEqual(1f, PlanGeometry.DistanceSq(new Vector3(1f, 0f, 0f), cable), 1e-5f);
            Assert.Greater(PlanGeometry.DistanceSq(new Vector3(1f, 0f, 0f), cable), RevealDistanceSq);
        }

        [Test]
        public void AStudIsFoundAnywhereOverItsFootprint()
        {
            var stud = new ScannedStructure
            {
                Type = StructureType.Stud,
                LocalPosition = Vector3.zero,
                Path = new List<Vector3>()
            };

            // The stud is 1.6 m tall, so 0.7 m above its center is still inside it.
            Assert.AreEqual(0f, PlanGeometry.DistanceSq(new Vector3(0f, 0.7f, 0f), stud), 1e-6f);
            Assert.Greater(PlanGeometry.DistanceSq(new Vector3(2f, 0f, 0f), stud), RevealDistanceSq);
        }

        [Test]
        public void TwoRouteNodesOnTheSameSpotAreNotALeg()
        {
            var node = new Vector3(0.2f, 0.3f, 0f);

            Assert.IsFalse(PlanGeometry.IsRouteLegLongEnough(node, node));
            Assert.IsTrue(PlanGeometry.IsRouteLegLongEnough(node, node + new Vector3(0.05f, 0f, 0f)));
        }
    }
}
