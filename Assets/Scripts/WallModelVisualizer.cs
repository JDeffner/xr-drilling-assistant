using UnityEngine;
using UnityEngine.Rendering;

namespace DrillingAssistant
{
    /// <summary>
    /// Attached to each user-marker visual so the VR editor can find the
    /// model entry belonging to a raycast hit.
    /// </summary>
    public class MarkerRef : MonoBehaviour
    {
        public int MarkerId;
    }

    /// <summary>Same as MarkerRef, for user-drawn cables.</summary>
    public class CableRef : MonoBehaviour
    {
        public int CableId;
    }

    /// <summary>
    /// Builds the visual representation of a ScannedWallModel under a parent
    /// transform, in wall-local coordinates. Used by OverlayProjector (C4,
    /// parent = real wall anchor) and VRWallEditor (C3, parent = grabbable copy).
    /// </summary>
    public static class WallModelVisualizer
    {
        private const float SurfaceOffset = 0.01f;
        private const float CableThickness = 0.03f;

        public static readonly Color CableColor = new Color(0.1f, 0.85f, 0.9f);

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>Semi-transparent wall quad, only used for the VR copy.</summary>
        public static void BuildWallQuad(Transform parent, Vector2 size, bool withCollider)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "WallQuad";
            // The quad's flat MeshCollider is only raycast-hittable from its
            // front face; a thin box works from both sides.
            Object.Destroy(quad.GetComponent<Collider>());
            if (withCollider)
            {
                var box = quad.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 1f, 0.02f);
            }
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial =
                GetTransparentMaterial(new Color(0.75f, 0.78f, 0.85f, 0.35f));
        }

        public static void BuildStructures(Transform parent, ScannedWallModel model, bool revealedOnly)
        {
            foreach (var s in model.Structures)
            {
                if (revealedOnly && !s.Revealed) continue;

                var color = StructureCatalog.GetColor(s.Type);
                // In VR, registered-but-unrevealed structures show as dim ghosts.
                var mat = s.Revealed
                    ? GetOpaqueMaterial(color)
                    : GetTransparentMaterial(new Color(color.r, color.g, color.b, 0.2f));

                if (s.Path != null && s.Path.Count >= 2)
                {
                    // Cable run: one segment per polyline leg.
                    for (int i = 1; i < s.Path.Count; i++)
                    {
                        var seg = BuildSegment(parent, $"Structure_{s.MarkerId}_{s.Type}_{i}",
                            s.Path[i - 1], s.Path[i], 0.04f, SurfaceOffset);
                        Object.Destroy(seg.GetComponent<Collider>());
                        seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    }
                    continue;
                }

                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Structure_{s.MarkerId}_{s.Type}";
                Object.Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(parent, false);
                box.transform.localPosition = s.LocalPosition + new Vector3(0f, 0f, SurfaceOffset);
                box.transform.localRotation = Quaternion.identity;
                box.transform.localScale = StructureCatalog.GetLocalSize(s.Type);
                box.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        /// <summary>
        /// A cube stretched between two wall-local points, lifted off the wall
        /// plane by zOffset. The caller decides material and whether the
        /// collider stays (user cables keep it for click-removal).
        /// </summary>
        private static GameObject BuildSegment(Transform parent, string name,
            Vector3 a, Vector3 b, float thickness, float zOffset)
        {
            Vector3 start = a + new Vector3(0f, 0f, zOffset);
            Vector3 end = b + new Vector3(0f, 0f, zOffset);
            Vector3 dir = end - start;

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = name;
            seg.transform.SetParent(parent, false);
            seg.transform.localPosition = (start + end) * 0.5f;
            if (dir.sqrMagnitude > 1e-8f)
            {
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
            seg.transform.localScale = new Vector3(thickness, dir.magnitude, thickness);
            return seg;
        }

        public static void BuildMarkers(Transform parent, ScannedWallModel model)
        {
            foreach (var m in model.Markers)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = $"Marker_{m.Id}";
                Object.Destroy(ring.GetComponent<Collider>());
                ring.AddComponent<SphereCollider>().radius = 0.6f;
                ring.AddComponent<MarkerRef>().MarkerId = m.Id;
                ring.transform.SetParent(parent, false);
                ring.transform.localPosition = m.LocalPosition + new Vector3(0f, 0f, SurfaceOffset * 2f);
                // Cylinder axis is Y; rotate so the disc lies flat on the wall.
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(0.08f, 0.004f, 0.08f);
                ring.GetComponent<MeshRenderer>().sharedMaterial =
                    GetOpaqueMaterial(new Color(0.1f, 0.9f, 0.2f));

                var label = new GameObject("Label");
                label.transform.SetParent(parent, false);
                label.transform.localPosition = m.LocalPosition + new Vector3(0f, 0.07f, SurfaceOffset * 3f);
                label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                label.transform.localScale = Vector3.one * 0.01f;
                var text = label.AddComponent<TextMesh>();
                text.text = $"M{m.Id}";
                text.fontSize = 48;
                text.color = new Color(0.1f, 0.9f, 0.2f);
                text.anchor = TextAnchor.LowerCenter;
                text.alignment = TextAlignment.Center;
            }
        }

        public static void BuildCables(Transform parent, ScannedWallModel model)
        {
            foreach (var c in model.Cables)
            {
                // The BoxCollider stays so the VR editor can hit it for removal.
                var seg = BuildSegment(parent, $"Cable_{c.Id}",
                    c.LocalStart, c.LocalEnd, CableThickness, SurfaceOffset * 2f);
                seg.AddComponent<CableRef>().CableId = c.Id;
                seg.GetComponent<MeshRenderer>().sharedMaterial = GetOpaqueMaterial(CableColor);
            }
        }

        private static Material _opaqueBase;
        private static Material _transparentBase;

        /// <summary>
        /// Null-safe shader lookup: shaders resolved only via Shader.Find can be
        /// stripped from a player build. Falling back keeps a missing shader from
        /// throwing inside a MonoBehaviour Start (which would leave the component
        /// half-initialized and throw every frame afterwards).
        /// </summary>
        public static Shader UnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        public static Material GetOpaqueMaterial(Color color)
        {
            if (_opaqueBase == null)
            {
                _opaqueBase = new Material(UnlitShader());
                // Double-sided: quads must be visible regardless of which way
                // the wall anchor's Z axis points.
                _opaqueBase.SetInt("_Cull", 0);
            }
            var mat = new Material(_opaqueBase);
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static Material GetTransparentMaterial(Color color)
        {
            if (_transparentBase == null)
            {
                _transparentBase = new Material(UnlitShader());
                _transparentBase.SetFloat("_Surface", 1f);
                _transparentBase.SetOverrideTag("RenderType", "Transparent");
                _transparentBase.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _transparentBase.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _transparentBase.SetInt("_ZWrite", 0);
                _transparentBase.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _transparentBase.renderQueue = (int)RenderQueue.Transparent;
                _transparentBase.SetInt("_Cull", 0);
            }
            var mat = new Material(_transparentBase);
            mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
