using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// C4: projects the model back onto the real wall as an AR overlay. Always
    /// instantiates from the wall-local data at identity under the wall anchor,
    /// so whatever was rotated/scaled in VR stays truthful to the physical wall.
    /// Only revealed structures are shown; user markers and routes are always shown.
    /// </summary>
    public class OverlayProjector : MonoBehaviour
    {
        public ScannedWallModel Model;

        private Transform _root;

        private void OnEnable()
        {
            if (Model == null) return;
            Model.Rebuilt += Rebuild;
            Model.MarkerAdded += OnMarkerAdded;
            Model.MarkerRemoved += OnMarkerRemoved;
            Model.RouteAdded += OnRouteAdded;
            Model.RouteRemoved += OnRouteRemoved;
            Rebuild();
        }

        private void OnDisable()
        {
            if (Model != null)
            {
                Model.Rebuilt -= Rebuild;
                Model.MarkerAdded -= OnMarkerAdded;
                Model.MarkerRemoved -= OnMarkerRemoved;
                Model.RouteAdded -= OnRouteAdded;
                Model.RouteRemoved -= OnRouteRemoved;
            }
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // A single marker or route only adds or destroys its own objects. Before
        // the overlay root exists there is nothing to update incrementally, so
        // the full build runs instead.
        private void OnMarkerAdded(UserMarker marker)
        {
            if (_root == null) Rebuild();
            else WallModelVisualizer.BuildMarker(_root, marker);
        }

        private void OnMarkerRemoved(int markerId)
        {
            if (_root != null) WallModelVisualizer.DestroyMarker(_root, markerId);
        }

        private void OnRouteAdded(PlannedRoute route)
        {
            if (_root == null) Rebuild();
            else WallModelVisualizer.BuildRoute(_root, route);
        }

        private void OnRouteRemoved(int routeId)
        {
            if (_root != null) WallModelVisualizer.DestroyRoute(_root, routeId);
        }

        private void Rebuild()
        {
            if (!Model.HasWall) return;

            if (_root == null)
            {
                _root = new GameObject("WallOverlay").transform;
            }
            // Pure re-projection: identity in the wall anchor's frame.
            _root.SetParent(Model.WallAnchor, false);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one;
            _root.gameObject.SetActive(true);

            WallModelVisualizer.Clear(_root);
            WallModelVisualizer.BuildStructures(_root, Model, revealedOnly: true);
            WallModelVisualizer.BuildMarkers(_root, Model);
            WallModelVisualizer.BuildRoutes(_root, Model);
        }
    }
}
