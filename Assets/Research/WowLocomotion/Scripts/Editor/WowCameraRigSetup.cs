using UnityEditor;
using UnityEngine;

namespace WowLocomotionResearch.Editor
{
    /// <summary>
    /// Editor utility that creates the WoW-like camera settings asset and reusable camera rig prefab.
    /// </summary>
    public static class WowCameraRigSetup
    {
        private const string ResearchFolder = "Assets/Research";
        private const string WowFolder = ResearchFolder + "/WowLocomotion";
        private const string AssetFolder = WowFolder + "/ScriptableObjects";
        private const string PrefabFolder = WowFolder + "/Prefabs";
        private const string CameraSettingsPath = AssetFolder + "/WowCameraSettings.asset";
        private const string MovementSettingsPath = AssetFolder + "/WowMovementSettings.asset";
        private const string CameraPrefabPath = PrefabFolder + "/WowCameraRig.prefab";

        /// <summary>
        /// Creates or updates the canonical camera and movement settings assets plus the reusable camera prefab.
        /// </summary>
        [MenuItem("Tools/Research/WoW Locomotion/Setup Camera Rig Prefab")]
        public static void SetupCameraRigPrefab()
        {
            EnsureFolders();

            var cameraSettings = LoadOrCreate<WowCameraSettings>(CameraSettingsPath);
            var movementSettings = LoadOrCreate<WowMovementSettings>(MovementSettingsPath);
            EditorUtility.SetDirty(cameraSettings);
            EditorUtility.SetDirty(movementSettings);

            var loadedExisting = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath) != null;
            var root = loadedExisting
                ? PrefabUtility.LoadPrefabContents(CameraPrefabPath)
                : new GameObject("WowCameraRig");

            try
            {
                ConfigureRig(root, cameraSettings);
                PrefabUtility.SaveAsPrefabAsset(root, CameraPrefabPath);
            }
            finally
            {
                if (loadedExisting)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured WoW camera rig prefab and settings assets.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ResearchFolder))
                AssetDatabase.CreateFolder("Assets", "Research");
            if (!AssetDatabase.IsValidFolder(WowFolder))
                AssetDatabase.CreateFolder(ResearchFolder, "WowLocomotion");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder(WowFolder, "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(WowFolder, "Prefabs");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigureRig(GameObject root, WowCameraSettings cameraSettings)
        {
            root.name = "WowCameraRig";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var rig = root.GetComponent<WowThirdPersonCameraRig>();
            if (rig == null)
                rig = root.AddComponent<WowThirdPersonCameraRig>();

            var cameraTransform = root.transform.Find("Camera");
            if (cameraTransform == null)
            {
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraTransform = cameraObject.transform;
            }

            var camera = cameraTransform.GetComponent<Camera>();
            if (camera == null)
                camera = cameraTransform.gameObject.AddComponent<Camera>();

            cameraTransform.localPosition = new Vector3(0f, 0f, -cameraSettings.CameraDistance);
            cameraTransform.localRotation = Quaternion.identity;
            cameraTransform.localScale = Vector3.one;
            camera.fieldOfView = cameraSettings.FieldOfView;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 1000f;
            cameraTransform.gameObject.tag = "MainCamera";

            var serialized = new SerializedObject(rig);
            serialized.FindProperty("settings").objectReferenceValue = cameraSettings;
            serialized.FindProperty("targetRoot").objectReferenceValue = null;
            serialized.FindProperty("explicitPivot").objectReferenceValue = null;
            serialized.FindProperty("controlledCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rig);
        }
    }
}
