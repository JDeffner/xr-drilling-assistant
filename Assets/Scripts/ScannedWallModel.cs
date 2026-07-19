using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrillingAssistant
{
    public enum StructureType
    {
        CableRun,
        Pipe,
        Stud
    }

    /// <summary>
    /// A hidden in-wall structure. Pose is wall-local (X right along the wall,
    /// Y up, Z into the room). MarkerId is just a unique id; the field name is
    /// kept so the saved plan format does not churn. Cable runs carry a Path
    /// polyline (they bend like real installations); pillars are a single
    /// box at LocalPosition.
    /// </summary>
    [Serializable]
    public class ScannedStructure
    {
        public int MarkerId;
        public StructureType Type;
        public Vector3 LocalPosition;
        public List<Vector3> Path = new List<Vector3>();
        public bool Revealed;
    }

    /// <summary>
    /// A drill/annotation marker the user placed in VR, wall-local like structures.
    /// </summary>
    [Serializable]
    public class UserMarker
    {
        public int Id;
        public Vector3 LocalPosition;
    }

    /// <summary>
    /// A planned cable run the user drew in VR between two free wall-local points.
    /// </summary>
    [Serializable]
    public class UserCable
    {
        public int Id;
        public Vector3 LocalStart;
        public Vector3 LocalEnd;
    }

    /// <summary>
    /// C2: the editable, wall-local data model of the scanned wall.
    /// Single source of truth for the AR overlay (C4) and the VR editor (C3).
    /// All poses live in the wall anchor's coordinate frame, so re-projection
    /// onto the real wall is a pure instantiation at identity.
    /// </summary>
    public class ScannedWallModel : MonoBehaviour
    {
        public Transform WallAnchor { get; private set; }
        public Vector2 WallSize { get; private set; } = Vector2.one;

        public readonly List<ScannedStructure> Structures = new List<ScannedStructure>();
        public readonly List<UserMarker> Markers = new List<UserMarker>();
        public readonly List<UserCable> Cables = new List<UserCable>();

        /// <summary>Raised whenever wall, structures or markers change.</summary>
        public event Action Changed;

        public bool HasWall => WallAnchor != null;

        private int _nextMarkerId = 1;
        private int _nextCableId = 1;

        public void SetWall(Transform anchor, Vector2 size)
        {
            WallAnchor = anchor;
            WallSize = size;
            Structures.Clear();
            Markers.Clear();
            Cables.Clear();
            RaiseChanged();
        }

        public void AddStructure(int markerId, StructureType type, Vector3 localPosition,
            List<Vector3> path = null)
        {
            Structures.Add(new ScannedStructure
            {
                MarkerId = markerId,
                Type = type,
                LocalPosition = localPosition,
                Path = path ?? new List<Vector3>(),
                Revealed = false
            });
            RaiseChanged();
        }

        /// <summary>Returns true if the structure was newly revealed.</summary>
        public bool RevealStructure(int markerId)
        {
            var s = Structures.Find(x => x.MarkerId == markerId);
            if (s == null || s.Revealed) return false;
            s.Revealed = true;
            RaiseChanged();
            return true;
        }

        public UserMarker AddMarker(Vector3 localPosition)
        {
            var marker = new UserMarker { Id = _nextMarkerId++, LocalPosition = localPosition };
            Markers.Add(marker);
            RaiseChanged();
            return marker;
        }

        public void RemoveMarker(int id)
        {
            int removed = Markers.RemoveAll(m => m.Id == id);
            if (removed > 0) RaiseChanged();
        }

        public UserCable AddCable(Vector3 localStart, Vector3 localEnd)
        {
            var cable = new UserCable { Id = _nextCableId++, LocalStart = localStart, LocalEnd = localEnd };
            Cables.Add(cable);
            RaiseChanged();
            return cable;
        }

        public void RemoveCable(int id)
        {
            int removed = Cables.RemoveAll(c => c.Id == id);
            if (removed > 0) RaiseChanged();
        }

        /// <summary>C5 restore: replace all content in one step (single Changed event).</summary>
        public void RestoreState(List<ScannedStructure> structures, List<UserMarker> markers, List<UserCable> cables)
        {
            Structures.Clear();
            Structures.AddRange(structures);
            Markers.Clear();
            Markers.AddRange(markers);
            Cables.Clear();
            Cables.AddRange(cables);
            _nextMarkerId = 1;
            foreach (var m in Markers) _nextMarkerId = Mathf.Max(_nextMarkerId, m.Id + 1);
            _nextCableId = 1;
            foreach (var c in Cables) _nextCableId = Mathf.Max(_nextCableId, c.Id + 1);
            RaiseChanged();
        }

        public Vector3 WallToWorld(Vector3 localPosition) => WallAnchor.TransformPoint(localPosition);

        public Vector3 WorldToWall(Vector3 worldPosition) => WallAnchor.InverseTransformPoint(worldPosition);

        private void RaiseChanged() => Changed?.Invoke();
    }
}
