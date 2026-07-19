using Meta.XR.MRUtilityKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DrillingAssistant.EditorTools
{
    /// <summary>
    /// Builds and wires the complete Drilling Assistant scene setup in the open
    /// scene (idempotent, safe to re-run). Replaces manual inspector wiring so
    /// the setup is reproducible: MRUK loading scene data from the device,
    /// stage roots and all component references.
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("Tools/Drilling Assistant/Setup Scene")]
        public static void Run()
        {
            var rig = GameObject.Find("[BuildingBlock] Camera Rig");
            if (rig == null)
            {
                Debug.LogError("[SceneSetup] '[BuildingBlock] Camera Rig' not found. Open SampleScene first.");
                return;
            }

            var centerEye = FindDeep(rig.transform, "CenterEyeAnchor");
            var leftAnchor = FindDeep(rig.transform, "LeftControllerAnchor");
            var rightAnchor = FindDeep(rig.transform, "RightControllerAnchor");
            if (centerEye == null || leftAnchor == null || rightAnchor == null)
            {
                Debug.LogError("[SceneSetup] Camera rig anchors not found (CenterEye/LeftController/RightController).");
                return;
            }

            var passthroughGo = GameObject.Find("[BuildingBlock] Passthrough");
            var passthrough = passthroughGo != null ? passthroughGo.GetComponent<OVRPassthroughLayer>() : null;
            if (passthrough == null)
            {
                Debug.LogWarning("[SceneSetup] No OVRPassthroughLayer found; AR/VR toggle will only switch stages.");
            }

            // Fade for the Cut + Fade transition.
            var fade = GetOrAdd<OVRScreenFade>(centerEye.gameObject);
            fade.fadeOnStart = false;
            fade.fadeTime = 0.15f;

            // Leftover from the removed ArUco scanning pipeline.
            var trackerAnchor = GameObject.Find("TrackerAnchor");
            if (trackerAnchor != null) Object.DestroyImmediate(trackerAnchor);

            // MRUK: MrukBootstrap owns scene loading (permission wait, retries,
            // snapshot fallback), so the built-in auto-load stays off to avoid
            // two competing discoveries at startup.
            var mrukGo = GameObject.Find("MRUK") ?? new GameObject("MRUK");
            var mruk = GetOrAdd<MRUK>(mrukGo);
            if (mruk.SceneSettings == null) mruk.SceneSettings = new MRUK.MRUKSettings();
            mruk.SceneSettings.DataSource = MRUK.SceneDataSource.Device;
            mruk.SceneSettings.LoadSceneOnStartup = false;
            var bootstrap = GetOrAdd<MrukBootstrap>(mrukGo);

            // Link the saved room snapshot (if any) as the on-device fallback.
            // Written by Tools > Drilling Assistant > Save Room Snapshot.
            var snapshot = AssetDatabase.LoadAssetAtPath<TextAsset>(RoomSnapshotSaver.SnapshotAssetPath);
            if (snapshot != null) bootstrap.RoomSnapshot = snapshot;
            else Debug.Log("[SceneSetup] No room snapshot found at " + RoomSnapshotSaver.SnapshotAssetPath +
                           "; on-device fallback will be unavailable until one is saved.");

            // App root, components and stage roots.
            var appGo = GameObject.Find("Drilling Assistant") ?? new GameObject("Drilling Assistant");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(appGo);
            var model = GetOrAdd<ScannedWallModel>(appGo);
            var selector = GetOrAdd<WallSelector>(appGo);
            var transition = GetOrAdd<TransitionController>(appGo);
            var store = GetOrAdd<WallPlanStore>(appGo);
            var app = GetOrAdd<AppStateManager>(appGo);

            var arStage = GetOrCreateChild(appGo.transform, "AR Stage");
            var overlay = GetOrAdd<OverlayProjector>(arStage.gameObject);
            var revealer = GetOrAdd<StructureRevealer>(arStage.gameObject);
            var vrStage = GetOrCreateChild(appGo.transform, "VR Stage");
            var vrEditor = GetOrAdd<VRWallEditor>(vrStage.gameObject);
            vrStage.gameObject.SetActive(false);

            // Reference wiring.
            selector.RayOrigin = leftAnchor;
            selector.Model = model;

            transition.PassthroughLayer = passthrough;
            transition.ScreenFade = fade;
            transition.CenterCamera = centerEye.GetComponent<Camera>();

            overlay.Model = model;

            revealer.Model = model;
            revealer.LeftAnchor = leftAnchor;

            vrEditor.Model = model;
            vrEditor.LeftAnchor = leftAnchor;
            vrEditor.RightAnchor = rightAnchor;

            store.Model = model;

            app.WallSelector = selector;
            app.Revealer = revealer;
            app.Model = model;
            app.Transition = transition;
            app.Store = store;
            app.ArStageRoot = arStage.gameObject;
            app.VrStageRoot = vrStage.gameObject;

            foreach (var obj in new Object[]
                     {
                         fade, mruk, bootstrap, model, selector, transition, store,
                         app, overlay, revealer, vrEditor
                     })
            {
                EditorUtility.SetDirty(obj);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SceneSetup] Drilling Assistant scene setup complete.");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            return child;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
