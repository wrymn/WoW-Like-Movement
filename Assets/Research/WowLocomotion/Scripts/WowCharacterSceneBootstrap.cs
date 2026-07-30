using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Runtime scene bootstrap that spawns the globally selected character and camera at a scene spawn point.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class WowCharacterSceneBootstrap : MonoBehaviour
    {
        [Tooltip("Global character/camera spawn settings. The Selected Character dropdown decides which prefab is spawned.")]
        [SerializeField] private WowCharacterSpawnSettings settings;

        [Tooltip("Scene transform used as spawn position and rotation. If empty, Spawn Point Name from settings is searched, then this GameObject transform is used.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("When enabled, the selected character and camera spawn during Awake.")]
        [SerializeField] private bool spawnOnAwake = true;

        [Tooltip("When enabled, spawned camera rigs get a lightweight runtime FPS/VSync overlay for player builds and Play Mode.")]
        [SerializeField] private bool addRuntimePerformanceOverlay = true;

        private GameObject spawnedCharacter;
        private GameObject spawnedCameraRigObject;
        private WowThirdPersonCameraRig spawnedCameraRig;

        /// <summary>Global settings asset used by this bootstrap.</summary>
        public WowCharacterSpawnSettings Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        /// <summary>Scene transform used as the spawn position and rotation.</summary>
        public Transform SpawnPoint
        {
            get { return spawnPoint; }
            set { spawnPoint = value; }
        }

        /// <summary>Last character GameObject spawned by this bootstrap.</summary>
        public GameObject SpawnedCharacter
        {
            get { return spawnedCharacter; }
        }

        /// <summary>Last camera rig spawned by this bootstrap.</summary>
        public WowThirdPersonCameraRig SpawnedCameraRig
        {
            get { return spawnedCameraRig; }
        }

        /// <summary>True to add a runtime FPS and VSync overlay to spawned camera rigs.</summary>
        public bool AddRuntimePerformanceOverlay
        {
            get { return addRuntimePerformanceOverlay; }
            set { addRuntimePerformanceOverlay = value; }
        }

        private void Reset()
        {
            spawnPoint = transform;
        }

        private void Awake()
        {
            if (spawnOnAwake)
                SpawnSelectedCharacter();
        }

        /// <summary>
        /// Clears existing scene locomotion instances, spawns the selected character and camera, and wires their references.
        /// </summary>
        public void SpawnSelectedCharacter()
        {
            if (settings == null)
            {
                Debug.LogError("WowCharacterSceneBootstrap requires WowCharacterSpawnSettings.", this);
                return;
            }

            var characterPrefab = settings.SelectedCharacterPrefab;
            if (characterPrefab == null)
            {
                Debug.LogError($"No prefab assigned for selected character '{settings.SelectedCharacter}'.", settings);
                return;
            }

            var spawnTransform = ResolveSpawnPoint();
            if (settings.RemoveExistingSceneInstances)
                RemoveExistingSceneInstances();

            SpawnCamera(spawnTransform);

            spawnedCharacter = Instantiate(characterPrefab, spawnTransform.position, spawnTransform.rotation);
            spawnedCharacter.name = characterPrefab.name;

            var locomotion = spawnedCharacter.GetComponent<WowLikeAnimancerLocomotionPrototype>();
            if (locomotion == null)
                Debug.LogError($"Spawned character '{spawnedCharacter.name}' has no WowLikeAnimancerLocomotionPrototype on its root.", spawnedCharacter);

            WireCamera(spawnedCharacter.transform, locomotion);
        }

        private void SpawnCamera(Transform spawnTransform)
        {
            if (settings.CameraRigPrefab == null)
            {
                Debug.LogError("No camera rig prefab assigned in WowCharacterSpawnSettings.", settings);
                return;
            }

            spawnedCameraRigObject = Instantiate(settings.CameraRigPrefab, spawnTransform.position, spawnTransform.rotation);
            spawnedCameraRigObject.name = settings.CameraRigPrefab.name;
            spawnedCameraRig = spawnedCameraRigObject.GetComponent<WowThirdPersonCameraRig>();
            if (spawnedCameraRig == null)
            {
                Debug.LogError($"Spawned camera prefab '{spawnedCameraRigObject.name}' has no WowThirdPersonCameraRig on its root.", spawnedCameraRigObject);
                return;
            }

            EnsureRuntimePerformanceOverlay();
        }

        private void WireCamera(Transform targetRoot, WowLikeAnimancerLocomotionPrototype locomotion)
        {
            if (spawnedCameraRig == null)
                return;

            spawnedCameraRig.SetTarget(targetRoot);
            spawnedCameraRig.RecalculatePivotOffset();
            spawnedCameraRig.SnapToTargetImmediate();

            if (locomotion != null)
                locomotion.SetCamera(spawnedCameraRig);
        }

        private void EnsureRuntimePerformanceOverlay()
        {
            if (!addRuntimePerformanceOverlay || spawnedCameraRigObject == null)
                return;

            if (spawnedCameraRigObject.GetComponent<WowRuntimePerformanceOverlay>() == null)
                spawnedCameraRigObject.AddComponent<WowRuntimePerformanceOverlay>();
        }

        private Transform ResolveSpawnPoint()
        {
            if (spawnPoint != null)
                return spawnPoint;

            if (!string.IsNullOrWhiteSpace(settings.SpawnPointName))
            {
                var found = GameObject.Find(settings.SpawnPointName);
                if (found != null)
                {
                    spawnPoint = found.transform;
                    return spawnPoint;
                }
            }

            spawnPoint = transform;
            return spawnPoint;
        }

        private void RemoveExistingSceneInstances()
        {
            var locomotionControllers = FindObjectsByType<WowLikeAnimancerLocomotionPrototype>(FindObjectsSortMode.None);
            for (int i = 0; i < locomotionControllers.Length; i++)
            {
                var controller = locomotionControllers[i];
                if (controller != null)
                {
                    controller.gameObject.SetActive(false);
                    Destroy(controller.gameObject);
                }
            }

            var cameraRigs = FindObjectsByType<WowThirdPersonCameraRig>(FindObjectsSortMode.None);
            for (int i = 0; i < cameraRigs.Length; i++)
            {
                var rig = cameraRigs[i];
                if (rig != null)
                {
                    rig.gameObject.SetActive(false);
                    Destroy(rig.gameObject);
                }
            }
        }
    }
}
