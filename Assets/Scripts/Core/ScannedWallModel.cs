using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrillingAssistant
{
    public enum StructureType
    {
        CableRun = 0,
        // 1 was Pipe, which nothing ever generated. The values are explicit so
        // saved plans written before it was removed still read back correctly.
        Stud = 2
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
    /// One leg of a route the user planned in VR, between two free wall-local
    /// points. Distinct from StructureType.CableRun, which is an existing
    /// cable hidden in the wall.
    /// </summary>
    [Serializable]
    public class PlannedRoute
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
        public readonly List<PlannedRoute> Routes = new List<PlannedRoute>();

        /// <summary>Raised on every change, coarse enough for persistence.</summary>
        public event Action Changed;

        /// <summary>
        /// Raised when the content changed as a whole and visuals have to be
        /// built from scratch: a new wall, a restored plan, a new or revealed
        /// structure. Markers and routes report themselves one by one below, so
        /// a visual can add or remove a single object instead of rebuilding.
        /// </summary>
        public event Action Rebuilt;

        public event Action<UserMarker> MarkerAdded;
        public event Action<int> MarkerRemoved;
        public event Action<PlannedRoute> RouteAdded;
        public event Action<int> RouteRemoved;

        public bool HasWall => WallAnchor != null;

        private int _nextMarkerId = 1;
        private int _nextRouteId = 1;

        public void SetWall(Transform anchor, Vector2 size)
        {
            WallAnchor = anchor;
            WallSize = size;
            Structures.Clear();
            Markers.Clear();
            Routes.Clear();
            RaiseRebuilt();
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
            RaiseRebuilt();
        }

        /// <summary>Returns true if the structure was newly revealed.</summary>
        public bool RevealStructure(int markerId)
        {
            var s = Structures.Find(x => x.MarkerId == markerId);
            if (s == null || s.Revealed) return false;
            s.Revealed = true;
            RaiseRebuilt();
            return true;
        }

        public UserMarker AddMarker(Vector3 localPosition)
        {
            var marker = new UserMarker { Id = _nextMarkerId++, LocalPosition = localPosition };
            Markers.Add(marker);
            MarkerAdded?.Invoke(marker);
            RaiseChanged();
            return marker;
        }

        public void RemoveMarker(int id)
        {
            int removed = Markers.RemoveAll(m => m.Id == id);
            if (removed == 0) return;
            MarkerRemoved?.Invoke(id);
            RaiseChanged();
        }

        public PlannedRoute AddRoute(Vector3 localStart, Vector3 localEnd)
        {
            var route = new PlannedRoute { Id = _nextRouteId++, LocalStart = localStart, LocalEnd = localEnd };
            Routes.Add(route);
            RouteAdded?.Invoke(route);
            RaiseChanged();
            return route;
        }

        public void RemoveRoute(int id)
        {
            int removed = Routes.RemoveAll(r => r.Id == id);
            if (removed == 0) return;
            RouteRemoved?.Invoke(id);
            RaiseChanged();
        }

        /// <summary>C5 restore: replace all content in one step (single Changed event).</summary>
        public void RestoreState(List<ScannedStructure> structures, List<UserMarker> markers, List<PlannedRoute> routes)
        {
            Structures.Clear();
            Structures.AddRange(structures);
            Markers.Clear();
            Markers.AddRange(markers);
            Routes.Clear();
            Routes.AddRange(routes);
            _nextMarkerId = 1;
            foreach (var m in Markers) _nextMarkerId = Mathf.Max(_nextMarkerId, m.Id + 1);
            _nextRouteId = 1;
            foreach (var r in Routes) _nextRouteId = Mathf.Max(_nextRouteId, r.Id + 1);
            RaiseRebuilt();
        }

        public Vector3 WallToWorld(Vector3 localPosition) => WallAnchor.TransformPoint(localPosition);

        public Vector3 WorldToWall(Vector3 worldPosition) => WallAnchor.InverseTransformPoint(worldPosition);

        private void RaiseChanged() => Changed?.Invoke();

        private void RaiseRebuilt()
        {
            Rebuilt?.Invoke();
            Changed?.Invoke();
        }
    }
}
