using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// The wall-local geometry the plan is built on. Pure functions over the
    /// data model, with no scene or device state, so they can be tested in the
    /// editor without a headset.
    /// </summary>
    public static class PlanGeometry
    {
        /// <summary>
        /// Two route nodes closer than 2 cm are the same node pressed twice.
        /// </summary>
        public const float MinRouteLegSq = 0.0004f;

        public static bool IsRouteLegLongEnough(Vector3 a, Vector3 b)
        {
            return (b - a).sqrMagnitude > MinRouteLegSq;
        }

        /// <summary>
        /// Squared distance to the structure's body, not its center: cable runs
        /// follow their polyline path, studs their footprint box, so a sweep
        /// anywhere along them counts as a find.
        /// </summary>
        public static float DistanceSq(Vector3 probe, ScannedStructure structure)
        {
            var path = structure.Path;
            if (path != null && path.Count >= 2)
            {
                float best = float.MaxValue;
                for (int i = 1; i < path.Count; i++)
                {
                    best = Mathf.Min(best, SegmentDistanceSq(probe, path[i - 1], path[i]));
                }
                return best;
            }

            Vector3 half = StructureCatalog.GetLocalSize(structure.Type) * 0.5f;
            Vector3 d = probe - structure.LocalPosition;
            d.x = Mathf.Max(0f, Mathf.Abs(d.x) - half.x);
            d.y = Mathf.Max(0f, Mathf.Abs(d.y) - half.y);
            d.z = Mathf.Max(0f, Mathf.Abs(d.z) - half.z);
            return d.sqrMagnitude;
        }

        public static float SegmentDistanceSq(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f
                ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude)
                : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }
    }
}
