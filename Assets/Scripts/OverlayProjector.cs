using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// C4: projects the model back onto the real wall as an AR overlay. Always
    /// instantiates from the wall-local data at identity under the wall anchor,
    /// so whatever was rotated/scaled in VR stays truthful to the physical wall.
    /// Only revealed structures are shown; user markers and cables are always shown.
    /// </summary>
    public class OverlayProjector : MonoBehaviour
    {
        public ScannedWallModel Model;

        private Transform _root;

        private void OnEnable()
        {
            if (Model == null) return;
            Model.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (Model != null) Model.Changed -= Rebuild;
            if (_root != null) _root.gameObject.SetActive(false);
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
            WallModelVisualizer.BuildCables(_root, Model);
        }
    }
}
