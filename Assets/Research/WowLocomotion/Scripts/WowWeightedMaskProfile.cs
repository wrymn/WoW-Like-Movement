using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Named weighted mask groups used by the WoW-like locomotion research prototype.
    /// </summary>
    public enum WowWeightedMaskGroup
    {
        /// <summary>No bones are overridden by the upper-body layer.</summary>
        NoUpperBodyOverride = 0,

        /// <summary>Upper-body action while locomotion keeps the root, hips, and legs at zero weight.</summary>
        UpperBodyActionWhileMoving = 1,

        /// <summary>A lighter upper-body aim mask with reduced arm and spine influence.</summary>
        UpperBodyAimOnly = 2,

        /// <summary>Stationary full-body action while root and motion roots remain at zero weight.</summary>
        FullBodyActionWhileStationary = 3
    }

    /// <summary>
    /// Builds Animancer weighted mask tables from a Generic bone profile without Humanoid AvatarMask toggles.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WowWeightedMaskProfile",
        menuName = "Research/WoW Locomotion/Weighted Mask Profile")]
    public sealed class WowWeightedMaskProfile : ScriptableObject
    {
        /// <summary>Optional per-bone overrides applied after the default group table is generated.</summary>
        [Tooltip("Optional per-bone overrides applied after generated weights. Add entries only when a specific bone needs custom influence.")]
        public List<WowWeightedMaskBoneOverride> manualOverrides = new List<WowWeightedMaskBoneOverride>();

        /// <summary>
        /// Builds the desired bone-to-weight table for all weighted mask groups.
        /// </summary>
        /// <param name="boneProfile">Generic bone profile to use as the source of truth.</param>
        /// <returns>A list of desired weights for each assigned non-ignored bone.</returns>
        public List<WowWeightedMaskBoneWeights> BuildWeightTable(WowGenericBoneProfile boneProfile)
        {
            var table = new List<WowWeightedMaskBoneWeights>();
            if (boneProfile == null)
                return table;

            var bones = boneProfile.GetAllAssignedBones();
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone == null || Contains(boneProfile.ignoredBones, bone))
                    continue;

                var weights = new WowWeightedMaskBoneWeights(bone);
                weights.Set(WowWeightedMaskGroup.NoUpperBodyOverride, 0f);
                weights.Set(WowWeightedMaskGroup.UpperBodyActionWhileMoving, GetMovingActionWeight(boneProfile, bone));
                weights.Set(WowWeightedMaskGroup.UpperBodyAimOnly, GetAimOnlyWeight(boneProfile, bone));
                weights.Set(WowWeightedMaskGroup.FullBodyActionWhileStationary, GetStationaryFullBodyWeight(boneProfile, bone));
                ApplyManualOverrides(weights, bone);
                table.Add(weights);
            }

            return table;
        }

        /// <summary>
        /// Applies the generated table to an Animancer weighted mask definition using public APIs.
        /// </summary>
        /// <param name="definition">Weighted mask definition to configure.</param>
        /// <param name="boneProfile">Generic bone profile to use as the source of truth.</param>
        public void ApplyToDefinition(ref WeightedMaskLayersDefinition definition, WowGenericBoneProfile boneProfile)
        {
            var table = BuildWeightTable(boneProfile);
            var transforms = new Transform[table.Count];
            var groupCount = GetGroupCount();
            var weights = new float[table.Count * groupCount];
            var rootMotionWeights = new float[groupCount];

            for (int i = 0; i < table.Count; i++)
            {
                transforms[i] = table[i].bone;
                for (int group = 0; group < groupCount; group++)
                    weights[group * table.Count + i] = table[i].Get((WowWeightedMaskGroup)group);
            }

            for (int group = 0; group < groupCount; group++)
                rootMotionWeights[group] = 0f;

            definition.Transforms = transforms;
            definition.Weights = weights;
            definition.RootMotionWeights = rootMotionWeights;
            definition.Validate();
        }

        /// <summary>
        /// Applies the generated table to a WeightedMaskLayers component using Animancer public APIs.
        /// </summary>
        /// <param name="weightedMaskLayers">WeightedMaskLayers component to configure.</param>
        /// <param name="boneProfile">Generic bone profile to use as the source of truth.</param>
        public void ApplyToWeightedMaskLayers(WeightedMaskLayers weightedMaskLayers, WowGenericBoneProfile boneProfile)
        {
            if (weightedMaskLayers == null || boneProfile == null)
                return;

            ApplyToDefinition(ref weightedMaskLayers.Definition, boneProfile);
            if (weightedMaskLayers.LayerCount < 3)
                weightedMaskLayers.LayerCount = 3;
        }

        /// <summary>
        /// Gets the number of named weighted mask groups.
        /// </summary>
        /// <returns>The required group count.</returns>
        public static int GetGroupCount()
        {
            return 4;
        }

        private float GetMovingActionWeight(WowGenericBoneProfile profile, Transform bone)
        {
            if (IsRootOrLowerBody(profile, bone))
                return 0f;

            if (bone == profile.spineLower)
                return 0.20f;
            if (bone == profile.spineMiddle)
                return 0.45f;
            if (bone == profile.spineUpper)
                return 0.70f;
            if (bone == profile.chest)
                return 0.85f;
            if (bone == profile.neck)
                return 0.50f;
            if (bone == profile.head)
                return 0.35f;

            if (IsArmOrFinger(profile, bone) || Contains(profile.extraUpperBodyBones, bone))
                return 1f;

            return 0f;
        }

        private float GetAimOnlyWeight(WowGenericBoneProfile profile, Transform bone)
        {
            if (IsRootOrLowerBody(profile, bone))
                return 0f;

            if (bone == profile.spineLower)
                return 0.10f;
            if (bone == profile.spineMiddle)
                return 0.25f;
            if (bone == profile.spineUpper)
                return 0.45f;
            if (bone == profile.chest)
                return 0.60f;
            if (bone == profile.neck)
                return 0.45f;
            if (bone == profile.head)
                return 0.30f;
            if (IsClavicleOrShoulder(profile, bone))
                return 0.50f;
            if (bone == profile.leftUpperArm || bone == profile.rightUpperArm)
                return 0.35f;
            if (bone == profile.leftForearm || bone == profile.rightForearm)
                return 0.25f;
            if (bone == profile.leftHand || bone == profile.rightHand ||
                Contains(profile.leftFingers, bone) ||
                Contains(profile.rightFingers, bone))
                return 0.20f;
            if (Contains(profile.extraUpperBodyBones, bone))
                return 0.45f;

            return 0f;
        }

        private float GetStationaryFullBodyWeight(WowGenericBoneProfile profile, Transform bone)
        {
            if (bone == profile.skeletonRoot || bone == profile.armatureRoot || bone == profile.motionRoot)
                return 0f;

            return 1f;
        }

        private bool IsRootOrLowerBody(WowGenericBoneProfile profile, Transform bone)
        {
            if (bone == profile.skeletonRoot ||
                bone == profile.armatureRoot ||
                bone == profile.motionRoot ||
                bone == profile.pelvis ||
                bone == profile.hips ||
                bone == profile.leftThigh ||
                bone == profile.leftCalf ||
                bone == profile.leftFoot ||
                bone == profile.leftToe ||
                bone == profile.rightThigh ||
                bone == profile.rightCalf ||
                bone == profile.rightFoot ||
                bone == profile.rightToe ||
                Contains(profile.leftExtraLegBones, bone) ||
                Contains(profile.rightExtraLegBones, bone) ||
                Contains(profile.extraLowerBodyBones, bone))
                return true;

            return false;
        }

        private bool IsArmOrFinger(WowGenericBoneProfile profile, Transform bone)
        {
            return IsClavicleOrShoulder(profile, bone) ||
                bone == profile.leftUpperArm ||
                bone == profile.rightUpperArm ||
                bone == profile.leftForearm ||
                bone == profile.rightForearm ||
                bone == profile.leftHand ||
                bone == profile.rightHand ||
                Contains(profile.leftFingers, bone) ||
                Contains(profile.rightFingers, bone);
        }

        private bool IsClavicleOrShoulder(WowGenericBoneProfile profile, Transform bone)
        {
            return bone == profile.leftClavicle ||
                bone == profile.rightClavicle ||
                bone == profile.leftShoulder ||
                bone == profile.rightShoulder;
        }

        private void ApplyManualOverrides(WowWeightedMaskBoneWeights weights, Transform bone)
        {
            if (manualOverrides == null)
                return;

            for (int i = 0; i < manualOverrides.Count; i++)
            {
                var item = manualOverrides[i];
                if (item != null && item.bone == bone)
                    weights.Set(item.group, Mathf.Clamp01(item.weight));
            }
        }

        private static bool Contains(List<Transform> transforms, Transform transform)
        {
            return transforms != null && transforms.Contains(transform);
        }
    }

    /// <summary>
    /// Optional per-bone override for a specific weighted mask group.
    /// </summary>
    [System.Serializable]
    public sealed class WowWeightedMaskBoneOverride
    {
        /// <summary>Bone whose generated weight should be overridden.</summary>
        [Tooltip("Bone whose generated mask weight should be overridden.")]
        public Transform bone;

        /// <summary>Weighted mask group to override.</summary>
        [Tooltip("Mask group where this bone override applies.")]
        public WowWeightedMaskGroup group;

        /// <summary>Replacement weight clamped to the range [0, 1].</summary>
        [Tooltip("Replacement influence for this bone in the selected group. 0 means the upper layer has no effect; 1 means the upper layer fully controls this bone; values between blend lower and upper layers.")]
        [Range(0f, 1f)]
        public float weight;
    }

    /// <summary>
    /// Generated desired weights for one bone across all weighted mask groups.
    /// </summary>
    public sealed class WowWeightedMaskBoneWeights
    {
        /// <summary>Bone these weights apply to.</summary>
        public readonly Transform bone;

        private readonly float[] weights = new float[WowWeightedMaskProfile.GetGroupCount()];

        /// <summary>
        /// Creates a generated weight row for the specified bone.
        /// </summary>
        /// <param name="bone">Bone these weights apply to.</param>
        public WowWeightedMaskBoneWeights(Transform bone)
        {
            this.bone = bone;
        }

        /// <summary>
        /// Gets the weight for a named group.
        /// </summary>
        /// <param name="group">Weighted mask group.</param>
        /// <returns>Weight in the range [0, 1].</returns>
        public float Get(WowWeightedMaskGroup group)
        {
            return weights[(int)group];
        }

        /// <summary>
        /// Sets the weight for a named group.
        /// </summary>
        /// <param name="group">Weighted mask group.</param>
        /// <param name="weight">Weight in the range [0, 1].</param>
        public void Set(WowWeightedMaskGroup group, float weight)
        {
            weights[(int)group] = Mathf.Clamp01(weight);
        }
    }
}
