using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// C3: in VR the scanned wall model appears as a manipulable copy.
    /// Grip on one controller moves/rotates it, both grips scale it. You aim
    /// with the right controller ray. Right trigger places a drill marker on
    /// empty wall or deletes the marker it points at. Left trigger lays a pipe:
    /// each press drops a node and draws a segment from the previous one, so a
    /// run can be chained across many points; X ends the run. A erases the
    /// marker or pipe segment under the ray. Manipulation only ever changes this
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
        private LineRenderer _cablePreview;

        // wall-local start point of a cable being drawn, null when none started
        private Vector3? _pendingCableStart;

        // one-hand grab state
        private Transform _grabHand;
        private Vector3 _grabPosOffset;
        private Quaternion _grabRotOffset;

        // two-hand scale state
        private bool _twoHanded;
        private float _startHandDistance;
        private Vector3 _startScale;

        private void OnEnable()
        {
            if (Model == null || !Model.HasWall) return;
            EnsureRoot();
            RebuildContents();
            Model.Changed += RebuildContents;
        }

        private void OnDisable()
        {
            if (Model != null) Model.Changed -= RebuildContents;
            _grabHand = null;
            _twoHanded = false;
            _pendingCableStart = null;
            if (_cablePreview != null) _cablePreview.enabled = false;
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

            // Preview of the cable being drawn; needs its own GameObject since
            // a GameObject can hold only one LineRenderer.
            var previewGo = new GameObject("CablePreview");
            previewGo.transform.SetParent(transform, false);
            _cablePreview = WallSelector.CreateRayLine(previewGo, WallModelVisualizer.CableColor);

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
            WallModelVisualizer.BuildCables(_root, Model);
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
                _cablePreview.enabled = false;
                return;
            }

            var ray = new Ray(RightAnchor.position, RightAnchor.forward);
            bool hasHit = RaycastModel(ray, out RaycastHit hit);

            _ray.enabled = true;
            _ray.SetPosition(0, ray.origin);
            _ray.SetPosition(1, hasHit ? hit.point : ray.origin + ray.direction * 3f);

            UpdateCablePreview(hasHit, hit);

            // X ends the current pipe run (works even while pointing at nothing).
            if (OVRInput.GetDown(OVRInput.RawButton.X))
            {
                _pendingCableStart = null;
                return;
            }

            if (!hasHit) return;

            // A erases the marker or pipe segment under the ray.
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
                // Left trigger: drop the next pipe node.
                AddPipeNode(hit);
            }
        }

        private void UpdateCablePreview(bool hasHit, RaycastHit hit)
        {
            bool show = _pendingCableStart.HasValue && hasHit;
            _cablePreview.enabled = show;
            if (!show) return;
            _cablePreview.SetPosition(0, _root.TransformPoint(_pendingCableStart.Value));
            _cablePreview.SetPosition(1, hit.point);
        }

        private bool RaycastModel(Ray ray, out RaycastHit result)
        {
            result = default;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(ray, 10f))
            {
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

        private void AddPipeNode(RaycastHit hit)
        {
            // Pipe nodes are free points on the wall plane, independent of markers.
            // First press starts a run; each later press commits a segment from
            // the previous node and becomes the start of the next. X ends the run.
            Vector3 local = _root.InverseTransformPoint(hit.point);
            local.z = 0f;

            if (_pendingCableStart.HasValue &&
                (local - _pendingCableStart.Value).sqrMagnitude > 0.0004f)
            {
                // Ignore a zero-length segment (double press on the same spot).
                Model.AddCable(_pendingCableStart.Value, local);
            }
            _pendingCableStart = local;
        }

        private void RemoveAtHit(RaycastHit hit)
        {
            var markerRef = hit.collider.GetComponentInParent<MarkerRef>();
            if (markerRef != null)
            {
                Model.RemoveMarker(markerRef.MarkerId);
                return;
            }
            var cableRef = hit.collider.GetComponentInParent<CableRef>();
            if (cableRef != null) Model.RemoveCable(cableRef.CableId);
        }

    }
}
