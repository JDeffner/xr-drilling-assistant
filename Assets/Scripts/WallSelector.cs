using System;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// SelectWall state: point the left controller ray at an MRUK wall and
    /// confirm with the trigger. MrukBootstrap owns loading the scene data;
    /// this component only does the picking.
    /// </summary>
    public class WallSelector : MonoBehaviour
    {
        public Transform RayOrigin;
        public ScannedWallModel Model;

        [Tooltip("Max ray distance for wall picking.")]
        public float MaxDistance = 10f;

        public bool Active { get; set; }

        public event Action<MRUKAnchor> WallConfirmed;

        private LineRenderer _line;
        private GameObject _highlight;
        private MRUKAnchor _hovered;

        private void Start()
        {
            _line = CreateRayLine(gameObject, new Color(0.3f, 0.8f, 1f));

            _highlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _highlight.name = "WallHighlight";
            // Parented so the quad dies with its owner instead of staying
            // behind at the scene root.
            _highlight.transform.SetParent(transform, false);
            Destroy(_highlight.GetComponent<Collider>());
            _highlight.GetComponent<MeshRenderer>().sharedMaterial =
                WallModelVisualizer.GetOpaqueMaterial(new Color(0.3f, 0.8f, 1f, 1f));
            _highlight.SetActive(false);
        }

        internal static LineRenderer CreateRayLine(GameObject owner, Color color)
        {
            var line = owner.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.005f;
            line.endWidth = 0.002f;
            line.material = WallModelVisualizer.GetOpaqueMaterial(color);
            line.enabled = false;
            return line;
        }

        private void Update()
        {
            if (_line == null || _highlight == null || RayOrigin == null) return;

            if (!Active)
            {
                _line.enabled = false;
                _highlight.SetActive(false);
                _hovered = null;
                return;
            }

            var ray = GetRay();
            _hovered = null;
            Vector3 rayEnd = ray.origin + ray.direction * 3f;

            var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
            if (room != null && room.Raycast(ray, MaxDistance,
                    LabelFilter.Included(MRUKAnchor.SceneLabels.WALL_FACE),
                    out RaycastHit hit, out MRUKAnchor anchor))
            {
                _hovered = anchor;
                rayEnd = hit.point;
                ShowHighlight(anchor);
            }
            else
            {
                _highlight.SetActive(false);
            }

            _line.enabled = true;
            _line.SetPosition(0, ray.origin);
            _line.SetPosition(1, rayEnd);

            if (_hovered != null &&
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                Confirm(_hovered);
            }
        }

        private Ray GetRay()
        {
            return new Ray(RayOrigin.position, RayOrigin.forward);
        }

        private void ShowHighlight(MRUKAnchor anchor)
        {
            var rect = anchor.PlaneRect ?? new Rect(-0.5f, -0.5f, 1f, 1f);
            _highlight.SetActive(true);
            _highlight.transform.position =
                anchor.transform.TransformPoint(new Vector3(rect.center.x, rect.center.y, 0.005f));
            _highlight.transform.rotation = anchor.transform.rotation;
            _highlight.transform.localScale = new Vector3(rect.size.x, rect.size.y, 1f);
        }

        private void Confirm(MRUKAnchor anchor)
        {
            var size = anchor.PlaneRect?.size ?? Vector2.one;
            Model.SetWall(anchor.transform, size);
            _highlight.SetActive(false);
            _line.enabled = false;
            Debug.Log($"[WallSelector] Wall confirmed, size {size.x:F2} x {size.y:F2} m");
            WallConfirmed?.Invoke(anchor);
        }
    }
}
