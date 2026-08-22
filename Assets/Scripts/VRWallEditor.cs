using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// C3: in VR the scanned wall model appears as a manipulable copy.
    /// Grip on one controller moves/rotates it, both grips scale it. You aim
    /// with the right controller ray. Right trigger places a drill marker on
    /// empty wall or deletes the marker it points at. Left trigger lays a
    /// planned route: each press drops a node and draws a leg from the previous
    /// one, so a route can be chained across many points; X ends the route. A
    /// erases the marker or route leg under the ray. Manipulation only ever changes this
    /// copy's root transform; the wall-local data is never touched, so C4 stays
    /// truthful.
    /// </summary>
    public class VRWallEditor : MonoBehaviour
    {
        public ScannedWallModel Model;
        public Transform LeftAnchor;
        public Transform RightAnchor;

        private Transform _root;
        private LineRenderer _ray;
        private LineRenderer _routePreview;

        // wall-local start point of the route being drawn, null when none started
        private Vector3? _pendingRouteStart;

        // one-hand grab state
        private Transform _grabHand;
        private Vector3 _grabPosOffset;
        private Quaternion _grabRotOffset;

        // Reused so the per-frame raycast does not allocate a hit array. The
        // ray crosses the wall quad plus a handful of markers and route legs,
        // so this is far more than it ever needs.
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        // two-hand scale state
        private bool _twoHanded;
        private float _startHandDistance;
        private Vector3 _startScale;

        private void OnEnable()
        {
            if (Model == null || !Model.HasWall) return;
            EnsureRoot();
            RebuildContents();
            Subscribe(true);
        }

        private void OnDisable()
        {
            if (Model != null) Subscribe(false);
            _grabHand = null;
            _twoHanded = false;
            _pendingRouteStart = null;
            if (_routePreview != null) _routePreview.enabled = false;
        }

        /// <summary>
        /// A whole-content change rebuilds everything; a single marker or route
        /// only adds or destroys its own objects.
        /// </summary>
        private void Subscribe(bool on)
        {
            if (on)
            {
                Model.Rebuilt += RebuildContents;
                Model.MarkerAdded += OnMarkerAdded;
                Model.MarkerRemoved += OnMarkerRemoved;
                Model.RouteAdded += OnRouteAdded;
                Model.RouteRemoved += OnRouteRemoved;
                return;
            }
            Model.Rebuilt -= RebuildContents;
            Model.MarkerAdded -= OnMarkerAdded;
            Model.MarkerRemoved -= OnMarkerRemoved;
            Model.RouteAdded -= OnRouteAdded;
            Model.RouteRemoved -= OnRouteRemoved;
        }

        private void OnMarkerAdded(UserMarker marker)
        {
            if (_root != null) WallModelVisualizer.BuildMarker(_root, marker);
        }

        private void OnMarkerRemoved(int markerId)
        {
            if (_root != null) WallModelVisualizer.DestroyMarker(_root, markerId);
        }

        private void OnRouteAdded(PlannedRoute route)
        {
            if (_root != null) WallModelVisualizer.BuildRoute(_root, route);
        }

        private void OnRouteRemoved(int routeId)
        {
            if (_root != null) WallModelVisualizer.DestroyRoute(_root, routeId);
        }

        private void EnsureRoot()
        {
            if (_root != null) return;

            _root = new GameObject("WallModelCopy").transform;
            _root.SetParent(transform, false);

            // Spawn facing the user, 1.5 m ahead at eye height.
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
                _root.position = cam.transform.position + fwd * 1.5f;
                // Wall-local +Z points into the room, so aim it back at the user.
                _root.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            }

            _ray = WallSelector.CreateRayLine(gameObject, new Color(1f, 0.9f, 0.3f));

            // Preview of the route being drawn; needs its own GameObject since
            // a GameObject can hold only one LineRenderer.
            var previewGo = new GameObject("RoutePreview");
            previewGo.transform.SetParent(transform, false);
            _routePreview = WallSelector.CreateRayLine(previewGo, WallModelVisualizer.RouteColor);

            // Minimal VR environment: a dark floor disc so the void has a ground.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "VRFloor";
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(_root.position.x, 0f, _root.position.z);
            floor.transform.localScale = new Vector3(8f, 0.005f, 8f);
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                WallModelVisualizer.GetOpaqueMaterial(new Color(0.12f, 0.13f, 0.17f));
        }

        private void RebuildContents()
        {
            if (_root == null) return;
            WallModelVisualizer.Clear(_root);
            WallModelVisualizer.BuildWallQuad(_root, Model.WallSize, withCollider: true);
            // Show everything registered: revealed solid, unrevealed as ghosts.
            WallModelVisualizer.BuildStructures(_root, Model, revealedOnly: false);
            WallModelVisualizer.BuildMarkers(_root, Model);
            WallModelVisualizer.BuildRoutes(_root, Model);
        }

        private void Update()
        {
            if (_root == null) return;
            UpdateGrab();
            UpdateRay();
        }

        private void UpdateGrab()
        {
            bool leftGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
            bool rightGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

            if (leftGrip && rightGrip && LeftAnchor != null && RightAnchor != null)
            {
                float distance = Vector3.Distance(LeftAnchor.position, RightAnchor.position);
                if (!_twoHanded)
                {
                    _twoHanded = true;
                    _grabHand = null;
                    _startHandDistance = Mathf.Max(distance, 0.01f);
                    _startScale = _root.localScale;
                }
                _root.localScale = _startScale * (distance / _startHandDistance);
                return;
            }
            _twoHanded = false;

            Transform hand = leftGrip ? LeftAnchor : rightGrip ? RightAnchor : null;
            if (hand == null)
            {
                _grabHand = null;
                return;
            }

            if (_grabHand != hand)
            {
                _grabHand = hand;
                _grabPosOffset = hand.InverseTransformPoint(_root.position);
                _grabRotOffset = Quaternion.Inverse(hand.rotation) * _root.rotation;
            }
            _root.position = hand.TransformPoint(_grabPosOffset);
            _root.rotation = hand.rotation * _grabRotOffset;
        }

        private void UpdateRay()
        {
            if (RightAnchor == null || !OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
            {
                _ray.enabled = false;
                _routePreview.enabled = false;
                return;
            }

            var ray = new Ray(RightAnchor.position, RightAnchor.forward);
            bool hasHit = RaycastModel(ray, out RaycastHit hit);

            _ray.enabled = true;
            _ray.SetPosition(0, ray.origin);
            _ray.SetPosition(1, hasHit ? hit.point : ray.origin + ray.direction * 3f);

            UpdateRoutePreview(hasHit, hit);

            // X ends the current route (works even while pointing at nothing).
            if (OVRInput.GetDown(OVRInput.RawButton.X))
            {
                _pendingRouteStart = null;
                return;
            }

            if (!hasHit) return;

            // A erases the marker or route leg under the ray.
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                RemoveAtHit(hit);
                return;
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                // Right trigger: clicking an existing marker deletes it, empty wall places one.
                var markerRef = hit.collider.GetComponentInParent<MarkerRef>();
                if (markerRef != null) Model.RemoveMarker(markerRef.MarkerId);
                else PlaceMarker(hit);
            }
            else if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                // Left trigger: drop the next route node.
                AddRouteNode(hit);
            }
        }

        private void UpdateRoutePreview(bool hasHit, RaycastHit hit)
        {
            bool show = _pendingRouteStart.HasValue && hasHit;
            _routePreview.enabled = show;
            if (!show) return;
            _routePreview.SetPosition(0, _root.TransformPoint(_pendingRouteStart.Value));
            _routePreview.SetPosition(1, hit.point);
        }

        private bool RaycastModel(Ray ray, out RaycastHit result)
        {
            result = default;
            float best = float.MaxValue;
            int count = Physics.RaycastNonAlloc(ray, _hits, 10f);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (!hit.transform.IsChildOf(_root)) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    result = hit;
                }
            }
            return best < float.MaxValue;
        }

        private void PlaceMarker(RaycastHit hit)
        {
            // Contents live in wall-local coordinates directly under the root.
            Vector3 local = _root.InverseTransformPoint(hit.point);
            local.z = 0f;
            Model.AddMarker(local);
        }

        private void AddRouteNode(RaycastHit hit)
        {
            // Route nodes are free points on the wall plane, independent of
            // markers. First press starts a route; each later press commits a leg
            // from the previous node and becomes the start of the next. X ends it.
            Vector3 local = _root.InverseTransformPoint(hit.point);
            local.z = 0f;

            // A zero-length leg (double press on the same spot) is ignored.
            if (_pendingRouteStart.HasValue &&
                (local - _pendingRouteStart.Value).sqrMagnitude > 0.0004f)
            {
                Model.AddRoute(_pendingRouteStart.Value, local);
            }
            _pendingRouteStart = local;
        }

        private void RemoveAtHit(RaycastHit hit)
        {
            var markerRef = hit.collider.GetComponentInParent<MarkerRef>();
            if (markerRef != null)
            {
                Model.RemoveMarker(markerRef.MarkerId);
                return;
            }
            var routeRef = hit.collider.GetComponentInParent<RouteRef>();
            if (routeRef != null) Model.RemoveRoute(routeRef.RouteId);
        }

    }
}
