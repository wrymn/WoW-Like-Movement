using System;
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WowLocomotionResearch.Editor
{
    /// <summary>
    /// Editor setup utility for playable WoW locomotion character prefabs and scene bootstrap wiring.
    /// </summary>
    public static class WowPlayableCharacterSetup
    {
        private const string AssetFolder = "Assets/Research/WowLocomotion/ScriptableObjects";
        private const string ReboundClipFolder = AssetFolder + "/RootBoundClips/TaurenMale";
        private const string TaurenPrefabPath = "Assets/TaurenCharacter.prefab";
        private const string TaurenGlbPath = "Assets/Model/taurenmale.glb";
        private const string TaurenAnimSetPath = AssetFolder + "/TaurenMaleAnimSet.asset";
        private const string MovementSettingsPath = AssetFolder + "/WowMovementSettings.asset";
        private const string TaurenBoneProfilePath = AssetFolder + "/TaurenMaleBoneProfile.asset";
        private const string TaurenWeightedMaskProfilePath = AssetFolder + "/TaurenMaleWeightedMaskProfile.asset";
        private const string TaurenAvatarPath = AssetFolder + "/TaurenMaleGenericAvatar.asset";
        private const string SpawnSettingsPath = AssetFolder + "/WowCharacterSpawnSettings.asset";
        private const string HumanPrefabPath = "Assets/HumanCharacter.prefab";
        private const string CameraPrefabPath = "Assets/Research/WowLocomotion/Prefabs/WowCameraRig.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SpawnPointName = "Spawn Point";
        private const string TaurenSkeletonRootName = "taurenmale_skeleton";

        /// <summary>
        /// Creates/updates the Tauren character prefab, global spawn settings, and SampleScene bootstrap.
        /// </summary>
        [MenuItem("Tools/Research/WoW Locomotion/Setup Playable Characters And Spawner")]
        public static void SetupPlayableCharactersAndSpawner()
        {
            EnsureFolders();
            SetupTaurenCharacterPrefab();
            ConfigureSpawnSettings();
            ConfigureSampleSceneBootstrap();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured playable WoW characters and scene spawner.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Research"))
                AssetDatabase.CreateFolder("Assets", "Research");
            if (!AssetDatabase.IsValidFolder("Assets/Research/WowLocomotion"))
                AssetDatabase.CreateFolder("Assets/Research", "WowLocomotion");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets/Research/WowLocomotion", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(AssetFolder + "/RootBoundClips"))
                AssetDatabase.CreateFolder(AssetFolder, "RootBoundClips");
            if (!AssetDatabase.IsValidFolder(ReboundClipFolder))
                AssetDatabase.CreateFolder(AssetFolder + "/RootBoundClips", "TaurenMale");
        }

        private static void SetupTaurenCharacterPrefab()
        {
            var sourceClips = LoadClips(TaurenGlbPath);
            var animSet = LoadOrCreate<WowLocomotionAnimSet>(TaurenAnimSetPath);
            var movementSettings = LoadOrCreate<WowMovementSettings>(MovementSettingsPath);
            var weightedMaskProfile = LoadOrCreate<WowWeightedMaskProfile>(TaurenWeightedMaskProfilePath);

            var glbRoot = AssetDatabase.LoadAssetAtPath<GameObject>(TaurenGlbPath);
            if (glbRoot == null)
                throw new InvalidOperationException($"Could not load {TaurenGlbPath}.");

            var glbSkeletonRoot = FindChild(glbRoot.transform, TaurenSkeletonRootName);
            if (glbSkeletonRoot == null)
                throw new InvalidOperationException($"Could not find {TaurenSkeletonRootName} in the imported GLB asset.");

            var persistentBoneProfile = LoadOrCreate<WowGenericBoneProfile>(TaurenBoneProfilePath);
            FillBoneProfile(persistentBoneProfile, glbSkeletonRoot);
            EditorUtility.SetDirty(persistentBoneProfile);

            var prefabRoot = LoadOrCreateTaurenPrefabContents(glbRoot);
            try
            {
                var skeletonRoot = FindChild(prefabRoot.transform, TaurenSkeletonRootName);
                if (skeletonRoot == null)
                    throw new InvalidOperationException($"Could not find {TaurenSkeletonRootName} in TaurenCharacter.prefab.");

                var prefabBoneProfile = ScriptableObject.CreateInstance<WowGenericBoneProfile>();
                FillBoneProfile(prefabBoneProfile, skeletonRoot);

                var modelRoot = prefabRoot.transform.childCount > 0 ? prefabRoot.transform.GetChild(0) : null;
                if (modelRoot == null)
                    throw new InvalidOperationException("TaurenCharacter.prefab does not have a model child.");

                FillAnimSet(animSet, name => ClipOptional(sourceClips, name, prefabRoot.transform, modelRoot));
                EditorUtility.SetDirty(animSet);

                RemoveAnimatorStackFromModel(modelRoot);
                var characterController = ConfigureCharacterController(prefabRoot);

                var controller = prefabRoot.GetComponent<WowLikeAnimancerLocomotionPrototype>();
                if (controller == null)
                    controller = prefabRoot.AddComponent<WowLikeAnimancerLocomotionPrototype>();

                var actionController = prefabRoot.GetComponent<WowUpperBodyActionController>();
                if (actionController == null)
                    actionController = prefabRoot.AddComponent<WowUpperBodyActionController>();

                var animator = prefabRoot.GetComponent<Animator>();
                if (animator == null)
                    animator = prefabRoot.AddComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = true;
                animator.avatar = CreateGenericAvatar(prefabRoot, skeletonRoot);

                var animancer = prefabRoot.GetComponent<AnimancerComponent>();
                if (animancer == null)
                    animancer = prefabRoot.AddComponent<AnimancerComponent>();

                var weightedMaskLayers = prefabRoot.GetComponent<WeightedMaskLayers>();
                if (weightedMaskLayers == null)
                    weightedMaskLayers = prefabRoot.AddComponent<WeightedMaskLayers>();

                var clipSource = prefabRoot.GetComponent<WowAnimSetClipSource>();
                if (clipSource == null)
                    clipSource = prefabRoot.AddComponent<WowAnimSetClipSource>();

                ConfigureAnimancer(animancer, animator);
                ConfigureWeightedMaskLayers(weightedMaskLayers, animancer, weightedMaskProfile, prefabBoneProfile);
                ConfigurePrototype(controller, animancer, weightedMaskLayers, animSet, movementSettings, persistentBoneProfile, weightedMaskProfile, characterController, prefabRoot);
                ConfigureActionController(actionController, controller, movementSettings);
                ConfigureClipSource(clipSource, animSet);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, TaurenPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static GameObject LoadOrCreateTaurenPrefabContents(GameObject glbRoot)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TaurenPrefabPath) != null)
                return PrefabUtility.LoadPrefabContents(TaurenPrefabPath);

            var root = new GameObject("TaurenCharacter");
            var model = PrefabUtility.InstantiatePrefab(glbRoot) as GameObject;
            if (model == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException($"Could not instantiate {TaurenGlbPath}.");
            }

            model.name = "taurenmale";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            model.transform.localScale = Vector3.one;
            return root;
        }

        private static void ConfigureSpawnSettings()
        {
            var settings = LoadOrCreate<WowCharacterSpawnSettings>(SpawnSettingsPath);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("humanMaleHdPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HumanPrefabPath);
            serialized.FindProperty("taurenMalePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TaurenPrefabPath);
            serialized.FindProperty("cameraRigPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
            serialized.FindProperty("spawnPointName").stringValue = SpawnPointName;
            serialized.FindProperty("removeExistingSceneInstances").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureSampleSceneBootstrap()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var spawnPoint = GameObject.Find(SpawnPointName);
            if (spawnPoint == null)
                throw new InvalidOperationException($"Could not find scene object '{SpawnPointName}' in {ScenePath}.");

            var bootstrap = spawnPoint.GetComponent<WowCharacterSceneBootstrap>();
            if (bootstrap == null)
                bootstrap = spawnPoint.AddComponent<WowCharacterSceneBootstrap>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WowCharacterSpawnSettings>(SpawnSettingsPath);
            serialized.FindProperty("spawnPoint").objectReferenceValue = spawnPoint.transform;
            serialized.FindProperty("spawnOnAwake").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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

        private static Dictionary<string, AnimationClip> LoadClips(string glbPath)
        {
            var clips = new Dictionary<string, AnimationClip>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clips.ContainsKey(clip.name))
                    clips.Add(clip.name, clip);
            }
            return clips;
        }

        private static void FillAnimSet(WowLocomotionAnimSet animSet, Func<string, AnimationClip> clip)
        {
            animSet.idle = clip("Stand (ID 0 variation 0)");
            animSet.runForward = clip("Run (ID 5 variation 0)");
            animSet.runBackward = clip("RunBackwards (ID 1146 variation 0)") ?? clip("Walkbackwards (ID 13 variation 0)");
            animSet.strafeLeft = clip("ShuffleLeft (ID 11 variation 0)");
            animSet.strafeRight = clip("ShuffleRight (ID 12 variation 0)");
            animSet.walkForward = clip("Walk (ID 4 variation 0)");
            animSet.walkBackward = clip("Walkbackwards (ID 13 variation 0)");
            animSet.walkStrafeLeft = animSet.strafeLeft;
            animSet.walkStrafeRight = animSet.strafeRight;
            animSet.jumpStart = clip("JumpStart (ID 37 variation 0)");
            animSet.jumpLoop = clip("Fall (ID 40 variation 0)");
            animSet.jumpLand = clip("JumpEnd (ID 39 variation 0)");
            animSet.turnLeft = null;
            animSet.turnRight = null;
            animSet.runForwardLeft = null;
            animSet.runForwardRight = null;
            animSet.runBackwardLeft = null;
            animSet.runBackwardRight = null;
            animSet.upperBodyIdle = clip("ReadyUnarmed (ID 25 variation 0)");
            animSet.cast = clip("SpellCastOmni (ID 54 variation 0)");
            animSet.attack = clip("AttackUnarmed (ID 16 variation 0)");
            animSet.aimPose = clip("ReadySpellDirected (ID 51 variation 0)");
            animSet.readyPose = clip("ReadyUnarmed (ID 25 variation 0)");
        }

        private static AnimationClip ClipOptional(
            Dictionary<string, AnimationClip> clips,
            string name,
            Transform prefabRoot,
            Transform sourceRoot)
        {
            clips.TryGetValue(name, out var clip);
            return clip != null ? CreateRootBoundClip(name, clip, prefabRoot, sourceRoot) : null;
        }

        private static AnimationClip CreateRootBoundClip(
            string sourceName,
            AnimationClip sourceClip,
            Transform prefabRoot,
            Transform sourceRoot)
        {
            var clipPath = $"{ReboundClipFolder}/{SanitizeAssetName(sourceName)}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            EditorUtility.CopySerialized(sourceClip, clip);
            clip.ClearCurves();
            clip.name = sourceName;
            RebindFloatCurves(sourceClip, clip, prefabRoot, sourceRoot);
            RebindObjectCurves(sourceClip, clip, prefabRoot, sourceRoot);
            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(sourceClip));
            var settings = AnimationUtility.GetAnimationClipSettings(sourceClip);
            if (IsOneShotClip(sourceName))
                settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static bool IsOneShotClip(string clipName)
        {
            return clipName == "SpellCastOmni (ID 54 variation 0)" ||
                clipName == "AttackUnarmed (ID 16 variation 0)";
        }

        private static void RebindFloatCurves(AnimationClip sourceClip, AnimationClip targetClip, Transform prefabRoot, Transform sourceRoot)
        {
            var bindings = AnimationUtility.GetCurveBindings(sourceClip);
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                binding.path = GetRootBoundPath(binding.path, prefabRoot, sourceRoot);
                AnimationUtility.SetEditorCurve(targetClip, binding, AnimationUtility.GetEditorCurve(sourceClip, bindings[i]));
            }
        }

        private static void RebindObjectCurves(AnimationClip sourceClip, AnimationClip targetClip, Transform prefabRoot, Transform sourceRoot)
        {
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(sourceClip);
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                binding.path = GetRootBoundPath(binding.path, prefabRoot, sourceRoot);
                AnimationUtility.SetObjectReferenceCurve(targetClip, binding, AnimationUtility.GetObjectReferenceCurve(sourceClip, bindings[i]));
            }
        }

        private static string GetRootBoundPath(string sourcePath, Transform prefabRoot, Transform sourceRoot)
        {
            var sourceTransform = string.IsNullOrEmpty(sourcePath) ? sourceRoot : sourceRoot.Find(sourcePath);
            if (sourceTransform == null)
                throw new InvalidOperationException($"Could not resolve animation binding path '{sourcePath}' under '{sourceRoot.name}'.");

            return AnimationUtility.CalculateTransformPath(sourceTransform, prefabRoot);
        }

        private static string SanitizeAssetName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                name = name.Replace(invalid[i], '_');
            return name;
        }

        private static void FillBoneProfile(WowGenericBoneProfile profile, Transform skeletonRoot)
        {
            profile.skeletonRoot = skeletonRoot;
            profile.armatureRoot = skeletonRoot;
            profile.motionRoot = FindChild(skeletonRoot, "bone_Root");
            profile.pelvis = FindFirst(skeletonRoot, "bone_EXP_C1_Pelvis1", "bone_Waist", "bone_Root");
            profile.hips = FindChild(skeletonRoot, "bone_Root");
            profile.spineLower = FindChild(skeletonRoot, "bone_SpineLow");
            profile.spineMiddle = FindFirst(skeletonRoot, "bone_EXP_C1_Spine2", "bone_SpineUp");
            profile.spineUpper = FindFirst(skeletonRoot, "bone_Chest", "bone_SpineUp");
            profile.chest = FindFirst(skeletonRoot, "bone_Chest", "bone_SpineUp");
            profile.neck = FindFirst(skeletonRoot, "bone_EXP_C1_Neck1", "bone_Neck");
            profile.head = FindChild(skeletonRoot, "bone_Head");

            profile.leftClavicle = FindChild(skeletonRoot, "bone_ShoulderL");
            profile.leftShoulder = FindChild(skeletonRoot, "bone_ShoulderL");
            profile.leftUpperArm = FindFirst(skeletonRoot, "bone_ArmL", "bone_UpperArmL");
            profile.leftForearm = FindFirst(skeletonRoot, "bone_ElbowL", "bone_ForearmL");
            profile.leftHand = FindChild(skeletonRoot, "bone_HandL");
            FillChildren(profile.leftFingers, profile.leftHand);

            profile.rightClavicle = FindChild(skeletonRoot, "bone_ShoulderR");
            profile.rightShoulder = FindChild(skeletonRoot, "bone_ShoulderR");
            profile.rightUpperArm = FindFirst(skeletonRoot, "bone_ArmR", "bone_UpperArmR");
            profile.rightForearm = FindFirst(skeletonRoot, "bone_ElbowR", "bone_ForearmR");
            profile.rightHand = FindChild(skeletonRoot, "bone_HandR");
            FillChildren(profile.rightFingers, profile.rightHand);

            profile.leftThigh = FindFirst(skeletonRoot, "bone_EXP_L1_Leg1Twist1", "bone_LegL");
            profile.leftCalf = FindFirst(skeletonRoot, "bone_EXP_L1_Leg2Twist1", "bone_CalfL");
            profile.leftFoot = FindChild(skeletonRoot, "bone_FootL");
            profile.leftToe = FindFirst(skeletonRoot, "bone_32", "bone_ToeL");
            FillList(profile.leftExtraLegBones,
                FindChild(skeletonRoot, "bone_EXP_L1_Leg1Twist3"),
                FindChild(skeletonRoot, "bone_EXP_L1_Leg2Twist1"));

            profile.rightThigh = FindFirst(skeletonRoot, "bone_EXP_R1_Leg1Twist1", "bone_LegR");
            profile.rightCalf = FindFirst(skeletonRoot, "bone_EXP_R1_Leg2Twist1", "bone_CalfR");
            profile.rightFoot = FindChild(skeletonRoot, "bone_FootR");
            profile.rightToe = FindFirst(skeletonRoot, "bone_31", "bone_ToeR");
            FillList(profile.rightExtraLegBones,
                FindChild(skeletonRoot, "bone_EXP_R1_Leg1Twist3"),
                FindChild(skeletonRoot, "bone_EXP_R1_Leg2Twist1"));

            FillList(profile.extraUpperBodyBones,
                FindChild(skeletonRoot, "bone_EXP_L1_Arm1Twist2"),
                FindChild(skeletonRoot, "bone_EXP_L1_Arm1Twist3"),
                FindChild(skeletonRoot, "bone_EXP_L1_Arm2Twist2"),
                FindChild(skeletonRoot, "bone_EXP_L1_Arm2Twist3"),
                FindChild(skeletonRoot, "bone_EXP_R1_Arm1Twist2"),
                FindChild(skeletonRoot, "bone_EXP_R1_Arm1Twist3"),
                FindChild(skeletonRoot, "bone_EXP_R1_Arm2Twist2"),
                FindChild(skeletonRoot, "bone_EXP_R1_Arm2Twist3"));
            profile.extraLowerBodyBones.Clear();
            profile.ignoredBones.Clear();
        }

        private static CharacterController ConfigureCharacterController(GameObject prefabRoot)
        {
            var controller = prefabRoot.GetComponent<CharacterController>();
            if (controller == null)
                controller = prefabRoot.AddComponent<CharacterController>();

            controller.height = 2.7f;
            controller.radius = 0.75f;
            controller.center = new Vector3(0f, 1.35f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.025f;
            controller.minMoveDistance = 0f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureAnimancer(AnimancerComponent animancer, Animator animator)
        {
            var serialized = new SerializedObject(animancer);
            serialized.FindProperty("_Animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animancer);
        }

        private static void ConfigureWeightedMaskLayers(
            WeightedMaskLayers weightedMaskLayers,
            AnimancerComponent animancer,
            WowWeightedMaskProfile weightedMaskProfile,
            WowGenericBoneProfile prefabBoneProfile)
        {
            var serialized = new SerializedObject(weightedMaskLayers);
            serialized.FindProperty("_Animancer").objectReferenceValue = animancer;
            serialized.FindProperty("_LayerCount").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            weightedMaskProfile.ApplyToWeightedMaskLayers(weightedMaskLayers, prefabBoneProfile);
            EditorUtility.SetDirty(weightedMaskLayers);
            EditorUtility.SetDirty(weightedMaskProfile);
        }

        private static void ConfigurePrototype(
            WowLikeAnimancerLocomotionPrototype controller,
            AnimancerComponent animancer,
            WeightedMaskLayers weightedMaskLayers,
            WowLocomotionAnimSet animSet,
            WowMovementSettings movementSettings,
            WowGenericBoneProfile boneProfile,
            WowWeightedMaskProfile weightedMaskProfile,
            CharacterController characterController,
            GameObject prefabRoot)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("animancer").objectReferenceValue = animancer;
            serialized.FindProperty("weightedMaskLayers").objectReferenceValue = weightedMaskLayers;
            serialized.FindProperty("animSet").objectReferenceValue = animSet;
            serialized.FindProperty("movementSettings").objectReferenceValue = movementSettings;
            serialized.FindProperty("boneProfile").objectReferenceValue = boneProfile;
            serialized.FindProperty("weightedMaskProfile").objectReferenceValue = weightedMaskProfile;
            serialized.FindProperty("characterRoot").objectReferenceValue = prefabRoot.transform;
            serialized.FindProperty("modelRoot").objectReferenceValue = prefabRoot.transform.GetChild(0);
            serialized.FindProperty("cameraTransform").objectReferenceValue = null;
            serialized.FindProperty("cameraRig").objectReferenceValue = null;
            serialized.FindProperty("characterController").objectReferenceValue = characterController;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureActionController(
            WowUpperBodyActionController actionController,
            WowLikeAnimancerLocomotionPrototype controller,
            WowMovementSettings movementSettings)
        {
            var serialized = new SerializedObject(actionController);
            serialized.FindProperty("locomotionPrototype").objectReferenceValue = controller;
            serialized.FindProperty("movementSettings").objectReferenceValue = movementSettings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actionController);
        }

        private static void ConfigureClipSource(WowAnimSetClipSource clipSource, WowLocomotionAnimSet animSet)
        {
            var serialized = new SerializedObject(clipSource);
            serialized.FindProperty("animSet").objectReferenceValue = animSet;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clipSource);
        }

        private static void RemoveAnimatorStackFromModel(Transform modelRoot)
        {
            var modelWeightedMasks = modelRoot.GetComponent<WeightedMaskLayers>();
            if (modelWeightedMasks != null)
                UnityEngine.Object.DestroyImmediate(modelWeightedMasks, true);

            var modelAnimancer = modelRoot.GetComponent<AnimancerComponent>();
            if (modelAnimancer != null)
                UnityEngine.Object.DestroyImmediate(modelAnimancer, true);

            var modelClipSource = modelRoot.GetComponent<WowAnimSetClipSource>();
            if (modelClipSource != null)
                UnityEngine.Object.DestroyImmediate(modelClipSource, true);

            var modelAnimator = modelRoot.GetComponent<Animator>();
            if (modelAnimator != null)
                UnityEngine.Object.DestroyImmediate(modelAnimator, true);
        }

        private static Avatar CreateGenericAvatar(GameObject prefabRoot, Transform skeletonRoot)
        {
            var avatar = AvatarBuilder.BuildGenericAvatar(prefabRoot, skeletonRoot.name);
            avatar.name = "TaurenMaleGenericAvatar";

            if (!avatar.isValid)
                throw new InvalidOperationException("Generated Tauren Generic Avatar is invalid.");
            if (avatar.isHuman)
                throw new InvalidOperationException("Generated Tauren Avatar is Humanoid, but this prototype requires Generic.");

            if (AssetDatabase.LoadAssetAtPath<Avatar>(TaurenAvatarPath) != null)
                AssetDatabase.DeleteAsset(TaurenAvatarPath);

            AssetDatabase.CreateAsset(avatar, TaurenAvatarPath);
            AssetDatabase.ImportAsset(TaurenAvatarPath);
            return AssetDatabase.LoadAssetAtPath<Avatar>(TaurenAvatarPath);
        }

        private static Transform FindFirst(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var found = FindChild(root, names[i]);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void FillChildren(List<Transform> list, Transform root)
        {
            list.Clear();
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
                list.Add(root.GetChild(i));
        }

        private static void FillList(List<Transform> list, params Transform[] transforms)
        {
            list.Clear();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && !list.Contains(transforms[i]))
                    list.Add(transforms[i]);
            }
        }
    }
}
