using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace DrillingAssistant
{
    public enum AppState
    {
        SelectWall,
        ScanAR,
        PlanVR
    }

    /// <summary>
    /// One scene, one state machine: SelectWall -> ScanAR &lt;-&gt; PlanVR, and
    /// Y in ScanAR returns to SelectWall to pick a different wall. Owns the
    /// AR/VR stage roots and listens for the right-controller B button to
    /// toggle between scanning (AR) and planning (VR) via Cut + Fade.
    /// </summary>
    public class AppStateManager : MonoBehaviour
    {
        [Header("Components")]
        public WallSelector WallSelector;
        public StructureRevealer Revealer;
        public ScannedWallModel Model;
        public TransitionController Transition;
        public WallPlanStore Store;

        [Header("Stage roots")]
        public GameObject ArStageRoot;
        public GameObject VrStageRoot;

        public AppState State { get; private set; }

        private void Start()
        {
            if (WallSelector != null) WallSelector.WallConfirmed += OnWallConfirmed;
            EnterSelectWall();
        }

        /// <summary>
        /// Wall selection is always the entry point; skipping it on restore
        /// proved too confusing (the app woke up in ScanAR showing nothing).
        /// On confirm, a saved plan for exactly this wall is restored (C5),
        /// otherwise the wall is seeded with fresh random structures.
        /// </summary>
        private void OnWallConfirmed(MRUKAnchor anchor)
        {
            if (Store == null || !Store.TryRestoreFor(anchor))
            {
                GenerateStructures();
            }
            EnterScanAR();
        }

        /// <summary>
        /// Seeds 3 to 5 hidden structures for the user to find: cables and
        /// pillars (studs), with the first two guaranteeing one of each.
        /// </summary>
        private void GenerateStructures()
        {
            Vector2 size = Model.WallSize;
            int count = Random.Range(3, 6); // 3 to 5 inclusive
            var placedPillars = new List<Rect>();
            for (int i = 0; i < count; i++)
            {
                bool cable = i == 0 || (i != 1 && Random.value < 0.5f);
                if (cable) AddCableRun(size, i);
                else AddPillar(size, i, placedPillars);
            }
        }

        /// <summary>
        /// Cables run like real installations instead of being vertical bars:
        /// they enter from the ceiling edge, drop vertically, elbow into a
        /// horizontal run, and sometimes drop again toward a socket.
        /// </summary>
        private void AddCableRun(Vector2 size, int id)
        {
            const float margin = 0.08f;
            float halfW = Mathf.Max(size.x * 0.5f - margin, 0.05f);
            float halfH = size.y * 0.5f;

            float x0 = Random.Range(-halfW, halfW);
            float y1 = Random.Range(-halfH + margin, halfH - 0.3f);
            float x1 = Mathf.Clamp(
                x0 + (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.4f, 1.2f), -halfW, halfW);

            var path = new List<Vector3>
            {
                new Vector3(x0, halfH, 0f),
                new Vector3(x0, y1, 0f),
                new Vector3(x1, y1, 0f)
            };
            // Half the cables drop once more, as if feeding a socket below.
            if (y1 - 0.3f > -halfH && Random.value < 0.5f)
            {
                path.Add(new Vector3(x1, Random.Range(-halfH + margin, y1 - 0.3f), 0f));
            }

            Model.AddStructure(id, StructureType.CableRun, path[1], path);
        }

        /// <summary>
        /// Pillars are single boxes placed on the wall plane, inset so their
        /// footprint stays inside the wall bounds, with a few rejection-sampling
        /// tries for best-effort non-overlap against other pillars. Cables may
        /// cross them, as in a real wall.
        /// </summary>
        private void AddPillar(Vector2 size, int id, List<Rect> placed)
        {
            var footprint = StructureCatalog.GetLocalSize(StructureType.Stud);
            float halfW = footprint.x * 0.5f;
            float halfH = footprint.y * 0.5f;
            float xLimit = size.x * 0.5f - halfW;
            float yLimit = size.y * 0.5f - halfH;

            Rect rect = default;
            Vector3 local = Vector3.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float x = xLimit > 0f ? Random.Range(-xLimit, xLimit) : 0f;
                float y = yLimit > 0f ? Random.Range(-yLimit, yLimit) : 0f;
                rect = new Rect(x - halfW, y - halfH, footprint.x, footprint.y);
                local = new Vector3(x, y, 0f);
                if (!placed.Exists(r => r.Overlaps(rect))) break;
            }
            placed.Add(rect);
            Model.AddStructure(id, StructureType.Stud, local);
        }

        private void Update()
        {
            if (State == AppState.SelectWall || Transition == null || Transition.IsTransitioning) return;

            // B button on the right controller toggles ScanAR <-> PlanVR.
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            {
                ToggleStage();
            }
            // Y button in ScanAR goes back to picking a (different) wall.
            else if (State == AppState.ScanAR && OVRInput.GetDown(OVRInput.RawButton.Y))
            {
                EnterSelectWall();
            }
        }

        public void ToggleStage()
        {
            if (State == AppState.SelectWall || Transition.IsTransitioning) return;
            bool toVR = State == AppState.ScanAR;
            Transition.RunTransition(() =>
            {
                if (toVR) ApplyPlanVR();
                else ApplyScanAR();
            });
        }

        private void EnterSelectWall()
        {
            State = AppState.SelectWall;
            Transition.SetPassthrough(true);
            ArStageRoot.SetActive(false);
            VrStageRoot.SetActive(false);
            WallSelector.Active = true;
            Revealer.Active = false;
            Debug.Log("[AppStateManager] State: SelectWall");
        }

        private void EnterScanAR()
        {
            WallSelector.Active = false;
            ApplyScanAR();
        }

        private void ApplyScanAR()
        {
            State = AppState.ScanAR;
            Transition.SetPassthrough(true);
            ArStageRoot.SetActive(true);
            VrStageRoot.SetActive(false);
            Revealer.Active = true;
            Debug.Log("[AppStateManager] State: ScanAR");
        }

        private void ApplyPlanVR()
        {
            State = AppState.PlanVR;
            Transition.SetPassthrough(false);
            ArStageRoot.SetActive(false);
            VrStageRoot.SetActive(true);
            Revealer.Active = false;
            Debug.Log("[AppStateManager] State: PlanVR");
        }
    }
}
