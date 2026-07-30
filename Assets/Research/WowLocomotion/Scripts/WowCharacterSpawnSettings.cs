using Sirenix.OdinInspector;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Global runtime selection and prefab references for spawning the WoW locomotion test character and camera.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WowCharacterSpawnSettings",
        menuName = "Research/WoW Locomotion/Character Spawn Settings")]
    [Searchable]
    public sealed class WowCharacterSpawnSettings : ScriptableObject
    {
        [BoxGroup("Selection")]
        [Tooltip("Playable character prefab to spawn at runtime. Change this dropdown to switch between Human and Tauren without editing the scene.")]
        [SerializeField] private WowPlayableCharacterKind selectedCharacter = WowPlayableCharacterKind.HumanMaleHd;

        [BoxGroup("Character Prefabs")]
        [Tooltip("Prefab used when Selected Character is Human Male HD. This prefab must have WowLikeAnimancerLocomotionPrototype on its root.")]
        [SerializeField] private GameObject humanMaleHdPrefab;

        [BoxGroup("Character Prefabs")]
        [Tooltip("Prefab used when Selected Character is Tauren Male. This prefab must have WowLikeAnimancerLocomotionPrototype on its root.")]
        [SerializeField] private GameObject taurenMalePrefab;

        [BoxGroup("Camera Prefab")]
        [Tooltip("Camera rig prefab spawned with the selected character. The bootstrap assigns its Target Root to the spawned character.")]
        [SerializeField] private GameObject cameraRigPrefab;

        [BoxGroup("Scene Spawn")]
        [Tooltip("Scene object name used when the bootstrap does not have an explicit Spawn Point transform assigned.")]
        [SerializeField] private string spawnPointName = "Spawn Point";

        [BoxGroup("Scene Spawn")]
        [Tooltip("When enabled, the bootstrap removes existing scene locomotion characters and camera rigs before spawning the selected prefabs. Keep enabled to avoid duplicate Human/Camera scene instances.")]
        [SerializeField] private bool removeExistingSceneInstances = true;

        /// <summary>Currently selected playable character kind.</summary>
        public WowPlayableCharacterKind SelectedCharacter
        {
            get { return selectedCharacter; }
            set { selectedCharacter = value; }
        }

        /// <summary>Prefab used for Human Male HD.</summary>
        public GameObject HumanMaleHdPrefab
        {
            get { return humanMaleHdPrefab; }
            set { humanMaleHdPrefab = value; }
        }

        /// <summary>Prefab used for Tauren Male.</summary>
        public GameObject TaurenMalePrefab
        {
            get { return taurenMalePrefab; }
            set { taurenMalePrefab = value; }
        }

        /// <summary>Camera rig prefab spawned and assigned to the selected character.</summary>
        public GameObject CameraRigPrefab
        {
            get { return cameraRigPrefab; }
            set { cameraRigPrefab = value; }
        }

        /// <summary>Fallback scene object name used to find the spawn point.</summary>
        public string SpawnPointName
        {
            get { return spawnPointName; }
            set { spawnPointName = value; }
        }

        /// <summary>True to remove existing scene locomotion characters and camera rigs before spawning.</summary>
        public bool RemoveExistingSceneInstances
        {
            get { return removeExistingSceneInstances; }
            set { removeExistingSceneInstances = value; }
        }

        /// <summary>Returns the prefab for <see cref="SelectedCharacter"/>.</summary>
        public GameObject SelectedCharacterPrefab
        {
            get { return GetCharacterPrefab(selectedCharacter); }
        }

        /// <summary>
        /// Returns the configured prefab for a playable character kind.
        /// </summary>
        /// <param name="character">Playable character kind to resolve.</param>
        public GameObject GetCharacterPrefab(WowPlayableCharacterKind character)
        {
            switch (character)
            {
                case WowPlayableCharacterKind.HumanMaleHd:
                    return humanMaleHdPrefab;
                case WowPlayableCharacterKind.TaurenMale:
                    return taurenMalePrefab;
                default:
                    return null;
            }
        }
    }
}
