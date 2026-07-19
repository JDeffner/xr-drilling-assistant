using System.Collections;
using Meta.XR.MRUtilityKit;
using UnityEngine;
#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace DrillingAssistant
{
    /// <summary>
    /// Robust room loading for the APK. MRUK's startup auto-load can fire before
    /// the scene permission is granted and then never retry, leaving zero rooms.
    /// This waits for the permission, then explicitly loads the scene from the
    /// device and keeps retrying until a room with wall anchors appears.
    ///
    /// On device the scanned room sometimes never arrives at all (a recurring
    /// Quest/OS quirk on this project), so as a last resort the APK loads a
    /// snapshot captured in the editor over Link. MRUK's JSON serialization
    /// preserves anchor UUIDs, so plans saved by WallPlanStore (keyed by wall
    /// anchor UUID) still match after the fallback. In the editor over Link we
    /// always use live device data and never fall back.
    /// </summary>
    public class MrukBootstrap : MonoBehaviour
    {
        private const string ScenePermission = "com.oculus.permission.USE_SCENE";

        [Tooltip("Room snapshot written by Tools > Drilling Assistant > Save Room Snapshot. " +
                 "Loaded on device only when live scene data does not arrive.")]
        public TextAsset RoomSnapshot;

        private IEnumerator Start()
        {
            while (MRUK.Instance == null)
            {
                yield return null;
            }

#if !UNITY_EDITOR && UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(ScenePermission))
            {
                Permission.RequestUserPermission(ScenePermission);
                // Wait until the user answers the system dialog.
                float waited = 0f;
                while (!Permission.HasUserAuthorizedPermission(ScenePermission) && waited < 60f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }
#endif

            // Give live device data a few short tries first. On Link this
            // succeeds on the first attempt; on device it usually never does.
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (RoomHasWalls()) yield break;

                var task = MRUK.Instance.LoadSceneFromDevice(true);
                while (!task.IsCompleted) yield return null;

                if (RoomHasWalls()) yield break;
                Debug.Log("[MrukBootstrap] LoadSceneFromDevice -> " + task.Result);
                yield return new WaitForSeconds(1.5f);
            }

#if !UNITY_EDITOR
            // Device data never arrived. Fall back to the saved snapshot so the
            // app stays usable on device. Anchor UUIDs survive the round-trip,
            // so a previously saved wall plan still restores.
            if (RoomSnapshot != null && !string.IsNullOrEmpty(RoomSnapshot.text))
            {
                var task = MRUK.Instance.LoadSceneFromJsonString(RoomSnapshot.text);
                while (!task.IsCompleted) yield return null;

                int roomCount = MRUK.Instance.Rooms != null ? MRUK.Instance.Rooms.Count : 0;
                Debug.Log("[MrukBootstrap] Snapshot fallback -> " + task.Result +
                          "; rooms=" + roomCount);
            }
            else
            {
                Debug.LogWarning("[MrukBootstrap] No RoomSnapshot assigned; cannot fall back to a saved room.");
            }
#endif
        }

        private static bool RoomHasWalls()
        {
            var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
            return room != null && room.WallAnchors.Count > 0;
        }
    }
}
