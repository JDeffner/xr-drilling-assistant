using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// Stud finder, active only in the AR state: the left controller is the
    /// physical detector. A hidden structure reveals itself once the left
    /// controller anchor comes within RevealDistance of it, with a short haptic
    /// pulse as detector feedback so the user feels the "find".
    /// </summary>
    public class StructureRevealer : MonoBehaviour
    {
        public Transform LeftAnchor;
        public ScannedWallModel Model;

        [Tooltip("Reveal distance between the left controller and a structure (meters).")]
        public float RevealDistance = 0.25f;

        public bool Active { get; set; }

        private float _hapticTimer;

        private void Update()
        {
            // Stop the pulse a moment after it started; a timer avoids a coroutine.
            if (_hapticTimer > 0f)
            {
                _hapticTimer -= Time.deltaTime;
                if (_hapticTimer <= 0f)
                {
                    OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
                }
            }

            if (!Active || Model == null || !Model.HasWall || LeftAnchor == null) return;

            float r2 = RevealDistance * RevealDistance;
            Vector3 probe = Model.WorldToWall(LeftAnchor.position);
            foreach (var s in Model.Structures)
            {
                if (s.Revealed) continue;
                if (DistanceSq(probe, s) > r2) continue;

                if (Model.RevealStructure(s.MarkerId))
                {
                    OVRInput.SetControllerVibration(0.7f, 0.8f, OVRInput.Controller.LTouch);
                    _hapticTimer = 0.12f;
                }
                break; // one reveal per frame keeps the pop-ins readable
            }
        }

        /// <summary>
        /// Wall-local squared distance to the structure's body, not its center:
        /// cables follow their polyline path, pillars their footprint box, so a
        /// sweep anywhere along them counts as a find.
        /// </summary>
        private static float DistanceSq(Vector3 probe, ScannedStructure s)
        {
            if (s.Path != null && s.Path.Count >= 2)
            {
                float best = float.MaxValue;
                for (int i = 1; i < s.Path.Count; i++)
                {
                    best = Mathf.Min(best, SegmentDistanceSq(probe, s.Path[i - 1], s.Path[i]));
                }
                return best;
            }

            Vector3 half = StructureCatalog.GetLocalSize(s.Type) * 0.5f;
            Vector3 d = probe - s.LocalPosition;
            d.x = Mathf.Max(0f, Mathf.Abs(d.x) - half.x);
            d.y = Mathf.Max(0f, Mathf.Abs(d.y) - half.y);
            d.z = Mathf.Max(0f, Mathf.Abs(d.z) - half.z);
            return d.sqrMagnitude;
        }

        private static float SegmentDistanceSq(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f
                ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude)
                : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }
    }
}
