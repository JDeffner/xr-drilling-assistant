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
                if (PlanGeometry.DistanceSq(probe, s) > r2) continue;

                if (Model.RevealStructure(s.MarkerId))
                {
                    OVRInput.SetControllerVibration(0.7f, 0.8f, OVRInput.Controller.LTouch);
                    _hapticTimer = 0.12f;
                }
                break; // one reveal per frame keeps the pop-ins readable
            }
        }
    }
}
