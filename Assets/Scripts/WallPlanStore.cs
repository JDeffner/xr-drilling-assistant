using System;
using System.Collections.Generic;
using System.IO;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// C5 save/load: one plan per wall, keyed by the wall anchor's UUID (which
    /// MRUK keeps stable across sessions, including snapshot-restored rooms).
    /// All plans live in one JSON file, so switching walls keeps every wall's
    /// structures, reveals, markers and routes. The file is read once at
    /// startup; each save replaces only the current wall's entry.
    /// </summary>
    public class WallPlanStore : MonoBehaviour
    {
        public ScannedWallModel Model;

        [Serializable]
        private class PlanDto
        {
            public string WallUuid;
            public List<ScannedStructure> Structures = new List<ScannedStructure>();
            public List<UserMarker> Markers = new List<UserMarker>();
            // Field name kept as "Cables": it is the key in the saved JSON, so
            // renaming it would drop the routes in plans saved before the rename.
            public List<PlannedRoute> Cables = new List<PlannedRoute>();
        }

        [Serializable]
        private class SaveFileDto
        {
            public List<PlanDto> Plans = new List<PlanDto>();
        }

        private SaveFileDto _file = new SaveFileDto();

        private string FilePath => Path.Combine(Application.persistentDataPath, "wallplans.json");

        private void Awake()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                var loaded = JsonUtility.FromJson<SaveFileDto>(File.ReadAllText(FilePath));
                if (loaded != null && loaded.Plans != null) _file = loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WallPlanStore] Load failed: " + e.Message);
            }
        }

        private void OnEnable()
        {
            if (Model != null) Model.Changed += Save;
        }

        private void OnDisable()
        {
            if (Model != null) Model.Changed -= Save;
        }

        /// <summary>
        /// Restores this wall's stored plan. Called after SetWall, so only the
        /// content lists need replacing. False when the wall has no plan yet.
        /// </summary>
        public bool TryRestoreFor(MRUKAnchor wall)
        {
            string uuid = wall.Anchor.Uuid.ToString();
            var plan = _file.Plans.Find(p => p.WallUuid == uuid);
            if (plan == null) return false;

            Model.RestoreState(plan.Structures, plan.Markers, plan.Cables);
            Debug.Log($"[WallPlanStore] Restored plan for wall {uuid}: " +
                      $"{plan.Structures.Count} structures, {plan.Markers.Count} markers, " +
                      $"{plan.Cables.Count} routes.");
            return true;
        }

        private void Save()
        {
            if (Model == null || !Model.HasWall) return;
            // SetWall clears the model before restore/generation runs; that
            // transient empty state must not overwrite the wall's stored plan.
            // A populated wall always has structures, so only the gap is skipped.
            if (Model.Structures.Count == 0) return;

            var anchor = Model.WallAnchor.GetComponent<MRUKAnchor>();
            if (anchor == null) return;
            string uuid = anchor.Anchor.Uuid.ToString();

            // Copied lists, not the model's own: the stored plan must survive
            // SetWall clearing the model when the user switches walls.
            var dto = new PlanDto
            {
                WallUuid = uuid,
                Structures = new List<ScannedStructure>(Model.Structures),
                Markers = new List<UserMarker>(Model.Markers),
                Cables = new List<PlannedRoute>(Model.Routes)
            };
            _file.Plans.RemoveAll(p => p.WallUuid == uuid);
            _file.Plans.Add(dto);

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_file));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WallPlanStore] Save failed: " + e.Message);
            }
        }
    }
}
