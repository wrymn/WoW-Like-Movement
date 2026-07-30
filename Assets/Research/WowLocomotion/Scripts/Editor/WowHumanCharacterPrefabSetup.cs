using System;
using System.Collections.Generic;
using Animancer;
using UnityEditor;
using UnityEngine;

namespace WowLocomotionResearch.Editor
{
    /// <summary>
    /// Editor utility that wires the local HumanCharacter prefab to the WoW locomotion research assets.
    /// </summary>
    public static class WowHumanCharacterPrefabSetup
    {
        private const string PrefabPath = "Assets/HumanCharacter.prefab";
        private const string GlbPath = "Assets/Model/humanmale_hd.glb";
        private const string AssetFolder = "Assets/Research/WowLocomotion/ScriptableObjects";
        private const string AnimSetPath = AssetFolder + "/HumanMaleHdAnimSet.asset";
        private const string MovementSettingsPath = AssetFolder + "/WowMovementSettings.asset";
        private const string BoneProfilePath = AssetFolder + "/HumanMaleHdBoneProfile.asset";
        private const string WeightedMaskProfilePath = AssetFolder + "/HumanMaleHdWeightedMaskProfile.asset";
        private const string AvatarPath = AssetFolder + "/HumanMaleHdGenericAvatar.asset";
        private const string ReboundClipFolder = AssetFolder + "/RootBoundClips";

        /// <summary>
        /// Creates/updates the research assets and assigns them to Assets/HumanCharacter.prefab.
        /// </summary>
        [MenuItem("Tools/Research/WoW Locomotion/Setup HumanCharacter Prefab")]
        public static void SetupHumanCharacterPrefab()
        {
            EnsureFolder();

            var sourceClips = LoadClips();
            var animSet = LoadOrCreate<WowLocomotionAnimSet>(AnimSetPath);
            var movementSettings = LoadOrCreate<WowMovementSettings>(MovementSettingsPath);
            EditorUtility.SetDirty(movementSettings);

            var weightedMaskProfile = LoadOrCreate<WowWeightedMaskProfile>(WeightedMaskProfilePath);
            EditorUtility.SetDirty(weightedMaskProfile);

            var glbRoot = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (glbRoot == null)
                throw new InvalidOperationException($"Could not load {GlbPath}.");

            var glbSkeletonRoot = FindChild(glbRoot.transform, "humanmale_hd_skeleton");
            if (glbSkeletonRoot == null)
                throw new InvalidOperationException("Could not find humanmale_hd_skeleton in the imported GLB asset.");

            var persistentBoneProfile = LoadOrCreate<WowGenericBoneProfile>(BoneProfilePath);
            FillBoneProfile(persistentBoneProfile, glbSkeletonRoot);
            EditorUtility.SetDirty(persistentBoneProfile);

            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var skeletonRoot = FindChild(prefabRoot.transform, "humanmale_hd_skeleton");
                if (skeletonRoot == null)
                    throw new InvalidOperationException("Could not find humanmale_hd_skeleton in HumanCharacter.prefab.");

                var prefabBoneProfile = ScriptableObject.CreateInstance<WowGenericBoneProfile>();
                FillBoneProfile(prefabBoneProfile, skeletonRoot);

                var modelRoot = prefabRoot.transform.childCount > 0 ? prefabRoot.transform.GetChild(0) : null;
                if (modelRoot == null)
                    throw new InvalidOperationException("HumanCharacter.prefab does not have a model child.");

                FillAnimSet(animSet, name => Clip(sourceClips, name, prefabRoot.transform, modelRoot));
                EditorUtility.SetDirty(animSet);

                RemoveAnimatorStackFromModel(modelRoot);

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
                ConfigurePrototype(controller, animancer, weightedMaskLayers, animSet, movementSettings, persistentBoneProfile, weightedMaskProfile, prefabRoot);
                ConfigureActionController(actionController, controller);
                ConfigureClipSource(clipSource, animSet);
                LogClipSourceCounts(clipSource, animancer);
                LogBindingResolution(prefabRoot, modelRoot, animSet.idle);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured HumanCharacter.prefab for WoW locomotion research.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Research"))
                AssetDatabase.CreateFolder("Assets", "Research");
            if (!AssetDatabase.IsValidFolder("Assets/Research/WowLocomotion"))
                AssetDatabase.CreateFolder("Assets/Research", "WowLocomotion");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets/Research/WowLocomotion", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(ReboundClipFolder))
                AssetDatabase.CreateFolder(AssetFolder, "RootBoundClips");
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

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(GlbPath);
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
            animSet.runBackward = clip("RunBackwards (ID 1146 variation 0)");
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

        private static AnimationClip Clip(
            Dictionary<string, AnimationClip> clips,
            string name,
            Transform prefabRoot,
            Transform sourceRoot)
        {
            clips.TryGetValue(name, out var clip);
            if (clip == null)
            {
                Debug.LogWarning($"GLB animation clip not found: {name}");
                return null;
            }

            return CreateRootBoundClip(name, clip, prefabRoot, sourceRoot);
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

        private static void RebindFloatCurves(
            AnimationClip sourceClip,
            AnimationClip targetClip,
            Transform prefabRoot,
            Transform sourceRoot)
        {
            var bindings = AnimationUtility.GetCurveBindings(sourceClip);
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                binding.path = GetRootBoundPath(binding.path, prefabRoot, sourceRoot);
                AnimationUtility.SetEditorCurve(targetClip, binding, AnimationUtility.GetEditorCurve(sourceClip, bindings[i]));
            }
        }

        private static void RebindObjectCurves(
            AnimationClip sourceClip,
            AnimationClip targetClip,
            Transform prefabRoot,
            Transform sourceRoot)
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
                throw new InvalidOperationException(
                    $"Could not resolve animation binding path '{sourcePath}' under '{sourceRoot.name}'.");

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
            profile.pelvis = FindChild(skeletonRoot, "bone_EXP_C1_Pelvis1");
            profile.hips = FindChild(skeletonRoot, "bone_Root");
            profile.spineLower = FindChild(skeletonRoot, "bone_SpineLow");
            profile.spineMiddle = FindChild(skeletonRoot, "bone_EXP_C1_Spine2");
            profile.spineUpper = FindChild(skeletonRoot, "bone_Chest");
            profile.chest = FindChild(skeletonRoot, "bone_Chest");
            profile.neck = FindChild(skeletonRoot, "bone_EXP_C1_Neck1");
            profile.head = FindChild(skeletonRoot, "bone_Head");

            profile.leftClavicle = FindChild(skeletonRoot, "bone_ShoulderL");
            profile.leftShoulder = FindChild(skeletonRoot, "bone_ShoulderL");
            profile.leftUpperArm = FindChild(skeletonRoot, "bone_ArmL");
            profile.leftForearm = FindChild(skeletonRoot, "bone_ElbowL");
            profile.leftHand = FindChild(skeletonRoot, "bone_HandL");
            FillChildren(profile.leftFingers, profile.leftHand);

            profile.rightClavicle = FindChild(skeletonRoot, "bone_ShoulderR");
            profile.rightShoulder = FindChild(skeletonRoot, "bone_ShoulderR");
            profile.rightUpperArm = FindChild(skeletonRoot, "bone_ArmR");
            profile.rightForearm = FindChild(skeletonRoot, "bone_ElbowR");
            profile.rightHand = FindChild(skeletonRoot, "bone_HandR");
            FillChildren(profile.rightFingers, profile.rightHand);

            profile.leftThigh = FindChild(skeletonRoot, "bone_EXP_L1_Leg1Twist1");
            profile.leftCalf = FindChild(skeletonRoot, "bone_EXP_L1_Leg2Twist1");
            profile.leftFoot = FindChild(skeletonRoot, "bone_FootL");
            profile.leftToe = FindChild(skeletonRoot, "bone_32");
            FillList(profile.leftExtraLegBones,
                FindChild(skeletonRoot, "bone_EXP_L1_Leg1Twist3"),
                FindChild(skeletonRoot, "bone_EXP_L1_Leg2Twist1"));

            profile.rightThigh = FindChild(skeletonRoot, "bone_EXP_R1_Leg1Twist1");
            profile.rightCalf = FindChild(skeletonRoot, "bone_EXP_R1_Leg2Twist1");
            profile.rightFoot = FindChild(skeletonRoot, "bone_FootR");
            profile.rightToe = FindChild(skeletonRoot, "bone_31");
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
        }

        private static void ConfigurePrototype(
            WowLikeAnimancerLocomotionPrototype controller,
            AnimancerComponent animancer,
            WeightedMaskLayers weightedMaskLayers,
            WowLocomotionAnimSet animSet,
            WowMovementSettings movementSettings,
            WowGenericBoneProfile boneProfile,
            WowWeightedMaskProfile weightedMaskProfile,
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
            serialized.FindProperty("characterController").objectReferenceValue = prefabRoot.GetComponent<CharacterController>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureActionController(
            WowUpperBodyActionController actionController,
            WowLikeAnimancerLocomotionPrototype controller)
        {
            var serialized = new SerializedObject(actionController);
            serialized.FindProperty("locomotionPrototype").objectReferenceValue = controller;
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

        private static void LogClipSourceCounts(WowAnimSetClipSource clipSource, AnimancerComponent animancer)
        {
            var clips = new List<AnimationClip>();
            clipSource.GetAnimationClips(clips);

            var animancerClips = new List<AnimationClip>();
            animancer.GetAnimationClips(animancerClips);

            Debug.Log(
                "WoW locomotion clip-source check: " +
                $"root={clips.Count}, animancer={animancerClips.Count}.");
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

        private static void LogBindingResolution(GameObject prefabRoot, Transform modelRoot, AnimationClip clip)
        {
            if (clip == null)
                return;

            var skeletonRoot = FindChild(prefabRoot.transform, "humanmale_hd_skeleton");
            var curves = AnimationUtility.GetCurveBindings(clip);
            var objects = AnimationUtility.GetObjectReferenceCurveBindings(clip);

            Debug.Log(
                $"Binding resolution for '{clip.name}': " +
                $"curves={curves.Length}, objectCurves={objects.Length}, " +
                $"HumanCharacter={CountResolved(prefabRoot.transform, curves)}, " +
                $"ModelRoot={CountResolved(modelRoot, curves)}, " +
                $"SkeletonRoot={CountResolved(skeletonRoot, curves)}");
        }

        private static int CountResolved(Transform root, EditorCurveBinding[] bindings)
        {
            if (root == null)
                return 0;

            var count = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (string.IsNullOrEmpty(bindings[i].path) || root.Find(bindings[i].path) != null)
                    count++;
            }
            return count;
        }

        private static Avatar CreateGenericAvatar(GameObject prefabRoot, Transform skeletonRoot)
        {
            var avatar = AvatarBuilder.BuildGenericAvatar(prefabRoot, skeletonRoot.name);
            avatar.name = "HumanMaleHdGenericAvatar";

            if (!avatar.isValid)
                throw new InvalidOperationException("Generated Generic Avatar is invalid.");
            if (avatar.isHuman)
                throw new InvalidOperationException("Generated Avatar is Humanoid, but this prototype requires Generic.");

            if (AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath) != null)
                AssetDatabase.DeleteAsset(AvatarPath);

            AssetDatabase.CreateAsset(avatar, AvatarPath);
            AssetDatabase.ImportAsset(AvatarPath);
            return AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
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
