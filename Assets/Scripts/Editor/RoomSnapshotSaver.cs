using System.IO;
using Meta.XR.MRUtilityKit;
using UnityEditor;
using UnityEngine;

namespace DrillingAssistant.EditorTools
{
    /// <summary>
    /// Saves the room MRUK currently has loaded to a JSON asset in the project.
    /// The developer runs this while playing in the editor over Link (where live
    /// scene data is available); the resulting file is the on-device fallback
    /// because the APK never receives scene data. MRUK's own JSON serialization
    /// is used so anchor UUIDs are preserved, which matters because WallPlanStore
    /// keys saved plans by wall anchor UUID.
    ///
    /// The TextAsset is wired onto MrukBootstrap by SceneSetup (edit mode, so it
    /// persists); after saving a new snapshot, re-run Tools > Drilling Assistant
    /// > Setup Scene to (re)link it.
    /// </summary>
    public static class RoomSnapshotSaver
    {
        /// <summary>Project-relative path of the saved snapshot; shared with SceneSetup.</summary>
        public const string SnapshotAssetPath = "Assets/RoomData/RoomSnapshot.json";

        [MenuItem("Tools/Drilling Assistant/Save Room Snapshot")]
        public static void Save()
        {
            if (!Application.isPlaying || MRUK.Instance == null)
            {
                Debug.LogWarning("[RoomSnapshotSaver] Enter Play mode over Link with a loaded room first.");
                return;
            }

            var room = MRUK.Instance.GetCurrentRoom();
            if (room == null || room.WallAnchors.Count == 0)
            {
                Debug.LogWarning("[RoomSnapshotSaver] No room with walls is loaded; nothing to save.");
                return;
            }

            // Native MRUK serialization keeps anchor UUIDs. Global mesh is not
            // used by this app, so leave it out to keep the file small. The
            // positional bool selects the non-obsolete overload (the deprecated
            // one takes a coordinate-system enum first).
            string json = MRUK.Instance.SaveSceneToJsonString(false);

            string dir = Path.GetDirectoryName(SnapshotAssetPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SnapshotAssetPath, json);
            AssetDatabase.Refresh();

            Debug.Log($"[RoomSnapshotSaver] Saved room snapshot to {SnapshotAssetPath}: " +
                      $"{MRUK.Instance.Rooms.Count} room(s), {room.WallAnchors.Count} wall(s). " +
                      "Re-run Tools > Drilling Assistant > Setup Scene to link it onto MrukBootstrap.");
        }
    }
}
